using System;
using TMPro;
using UnityEngine;

[DefaultExecutionOrder(100)]   // runs after CameraFollow so the bubble doesn't jitter
public class SpeechBubble : MonoBehaviour
{
    static SpeechBubble instance;

    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text bodyText;
    [SerializeField] RectTransform tail;          // optional: the little pointer, moved to face the NPC

    [Header("Position")]
    [SerializeField] float gap = 0.25f;           // world units between the NPC and the bubble
    [SerializeField] float moveSpeed = 12f;       // how fast the bubble slides to a new side
    [SerializeField] float switchMargin = 0.2f;   // higher = harder to switch sides (prevents flicker)
    [SerializeField] float screenMargin = 20f;    // minimum distance from the screen edge (pixels)

    static readonly Vector2[] sides =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right
    };

    // Where the tail sits on the bubble for each side (the edge facing the NPC)
    static readonly Vector2[] tailAnchors =
    {
        new Vector2(0.5f, 0f),   // bubble above the NPC  -> tail on the bottom edge
        new Vector2(0.5f, 1f),   // bubble below the NPC  -> tail on the top edge
        new Vector2(1f, 0.5f),   // bubble left of the NPC  -> tail on the right edge
        new Vector2(0f, 0.5f)    // bubble right of the NPC -> tail on the left edge
    };

    RectTransform panelRect;
    Transform target;
    Transform player;
    Vector2 targetExtents;        // half width/height of the NPC in world units
    Vector2 targetCenterOffset;   // NPC bounds center relative to its position
    Vector2 offsetNow;
    int currentSide;

    string[] lines;
    int index;
    float secondsPerLine;
    float timer;
    Action onFinished;

    public static bool IsShowing => instance != null && instance.panel.activeSelf;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        panelRect = panel.GetComponent<RectTransform>();
        panel.SetActive(false);
    }

    // Shows the lines one after another next to the target. Each line auto-advances after secondsPerLine.
    public static void Show(Transform target, string[] newLines, float secondsPerLine = 3f, Action finished = null)
    {
        if (instance == null)
        {
            Debug.LogWarning("SpeechBubble was not found!");
            finished?.Invoke();
            return;
        }

        instance.target = target;
        instance.lines = newLines;
        instance.index = 0;
        instance.secondsPerLine = secondsPerLine;
        instance.onFinished = finished;

        instance.MeasureTarget();
        instance.FindPlayer();
        instance.currentSide = instance.BestSide();
        instance.offsetNow = instance.OffsetForSide(instance.currentSide);

        instance.panel.SetActive(true);
        instance.ShowLine();
        instance.ApplyTail();
        instance.UpdatePosition();
    }

    // Skips to the next line (or finishes after the last one).
    public static void Skip()
    {
        if (IsShowing) instance.NextLine();
    }

    // Hides the bubble immediately without running the finished callback.
    public static void Hide()
    {
        if (instance != null) instance.Close();
    }

    void Update()
    {
        if (!panel.activeSelf) return;

        timer -= Time.deltaTime;
        if (timer <= 0f) NextLine();
    }

    void LateUpdate()
    {
        if (!panel.activeSelf || target == null) return;

        FindPlayer();
        UpdateSide();

        Vector2 desired = OffsetForSide(currentSide);
        float t = 1f - Mathf.Exp(-moveSpeed * Time.deltaTime);
        offsetNow = Vector2.Lerp(offsetNow, desired, t);

        UpdatePosition();
    }

    void ShowLine()
    {
        bodyText.text = lines[index];
        timer = secondsPerLine;
    }

    void NextLine()
    {
        index++;

        if (index >= lines.Length)
        {
            Action done = onFinished;
            Close();
            done?.Invoke();
        }
        else
        {
            ShowLine();
        }
    }

    void Close()
    {
        panel.SetActive(false);
        target = null;
        onFinished = null;
    }

    void FindPlayer()
    {
        if (player == null)
        {
            var p = FindAnyObjectByType<PlayerMovement>();
            if (p != null) player = p.transform;
        }
    }

    // Measures the NPC: sprite first, then collider, then a 1x1 fallback
    void MeasureTarget()
    {
        Bounds b;
        var r = target.GetComponentInChildren<Renderer>();

        if (r != null)
        {
            b = r.bounds;
        }
        else
        {
            var c = target.GetComponentInChildren<Collider2D>();
            b = c != null ? c.bounds : new Bounds(target.position, Vector3.one);
        }

        targetExtents = new Vector2(b.extents.x, b.extents.y);
        targetCenterOffset = (Vector2)b.center - (Vector2)target.position;
    }

    // The side that points most directly away from the player
    int BestSide()
    {
        if (player == null || target == null) return 0;

        Vector2 away = target.position - player.position;
        if (away.sqrMagnitude < 0.0001f) return currentSide;
        away.Normalize();

        int best = 0;
        float bestScore = float.MinValue;
        for (int i = 0; i < sides.Length; i++)
        {
            float score = Vector2.Dot(sides[i], away);
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }
        return best;
    }

    void UpdateSide()
    {
        if (player == null) return;

        Vector2 away = target.position - player.position;
        if (away.sqrMagnitude < 0.0001f) return;
        away.Normalize();

        int best = BestSide();
        float bestScore = Vector2.Dot(sides[best], away);
        float currentScore = Vector2.Dot(sides[currentSide], away);

        // Only switch when the new side is clearly better (avoids flicker on diagonals)
        if (best != currentSide && bestScore > currentScore + switchMargin)
        {
            currentSide = best;
            ApplyTail();
        }
    }

    // Moves the tail to the bubble edge that faces the NPC
    void ApplyTail()
    {
        if (tail == null) return;

        Vector2 a = tailAnchors[currentSide];
        tail.anchorMin = a;
        tail.anchorMax = a;
        tail.anchoredPosition = Vector2.zero;
    }

    // Half the panel size in world units (orthographic camera)
    Vector2 PanelHalfWorld(Camera cam)
    {
        float pixelsPerUnit = Screen.height / (2f * cam.orthographicSize);

        return new Vector2(
            panelRect.rect.width * panelRect.lossyScale.x,
            panelRect.rect.height * panelRect.lossyScale.y) * 0.5f / pixelsPerUnit;
    }

    Vector2 OffsetForSide(int side)
    {
        Camera cam = Camera.main;
        Vector2 panelHalf = cam != null ? PanelHalfWorld(cam) : Vector2.zero;

        float horizontal = targetExtents.x + panelHalf.x + gap;
        float vertical = targetExtents.y + panelHalf.y + gap;

        Vector2 dir = sides[side];
        return targetCenterOffset + new Vector2(dir.x * horizontal, dir.y * vertical);
    }

    void UpdatePosition()
    {
        Camera cam = Camera.main;
        if (cam == null || target == null) return;

        Vector3 world = target.position + (Vector3)offsetNow;
        Vector3 screen = cam.WorldToScreenPoint(world);

        // Keep the panel inside the screen
        Vector2 half = new Vector2(
            panelRect.rect.width * panelRect.lossyScale.x,
            panelRect.rect.height * panelRect.lossyScale.y) * 0.5f;

        screen.x = Mathf.Clamp(screen.x, half.x + screenMargin, Screen.width - half.x - screenMargin);
        screen.y = Mathf.Clamp(screen.y, half.y + screenMargin, Screen.height - half.y - screenMargin);

        panelRect.position = screen;
    }
}