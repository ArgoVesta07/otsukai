using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Top-right HUD: timer card, navigation arrow + errand card, incident "!" card.
// Layout, colors and sizes live on the UI objects; this script only fills them in.
[DefaultExecutionOrder(100)]   // runs after the player and camera have moved
public class GameHud : MonoBehaviour
{
    [Serializable]
    public class ErrandInfo
    {
        public QuestManager.QuestStatus status;
        public string shortText;              // e.g. "Buy eggs"
        [TextArea(2, 4)] public string detailText;   // shown when the card is expanded
        public string navTargetId;            // NavTarget id the arrow points to (empty = no arrow)
    }

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
    [SerializeField] ErrandInfo[] errands;

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

        UpdateTimer(quest);
        ErrandInfo info = UpdateErrand(quest);
        UpdateArrow(info);

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

        if (errandDetail != null)
            errandDetail.SetActive(expanded);

        RebuildLayout();
    }

    void UpdateTimer(QuestManager quest)
    {
        bool show = quest != null &&
                    (quest.TimerRunning || quest.currentStatus == QuestManager.QuestStatus.Failed);

        SetActive(timerCard, show);
        if (!show) return;

        int seconds = Mathf.CeilToInt(quest.TimeLeft);
        timerText.text = string.Format("{0:00}:{1:00}", seconds / 60, seconds % 60);
        timerText.color = quest.TimeLeft <= warningSeconds ? timerWarningColor : timerNormalColor;
    }

    ErrandInfo UpdateErrand(QuestManager quest)
    {
        ErrandInfo info = quest != null ? FindErrand(quest.currentStatus) : null;
        bool show = info != null && !string.IsNullOrEmpty(info.shortText);

        SetActive(errandRow, show);
        if (!show) return null;

        if (errandShortText.text != info.shortText)
        {
            errandShortText.text = info.shortText;
            errandDetailText.text = info.detailText;
            RebuildLayout();
        }

        return info;
    }

    void UpdateArrow(ErrandInfo info)
    {
        if (navArrow == null) return;

        if (player == null)
        {
            var p = FindAnyObjectByType<PlayerMovement>();
            if (p != null) player = p.transform;
        }

        NavTarget target = null;
        if (info != null && !string.IsNullOrEmpty(info.navTargetId))
        {
            target = NavTarget.Find(info.navTargetId);
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

    ErrandInfo FindErrand(QuestManager.QuestStatus status)
    {
        if (errands == null) return null;

        foreach (ErrandInfo e in errands)
        {
            if (e.status == status) return e;
        }

        return null;
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
