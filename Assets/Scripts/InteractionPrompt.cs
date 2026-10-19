using UnityEngine;
using TMPro;

[DefaultExecutionOrder(100)]   // runs after CameraFollow to prevent shaking
public class InteractionPrompt : MonoBehaviour
{
    static InteractionPrompt instance;

    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text label;

    [Header("Position")]
    [SerializeField] float gap = 0.15f;            // gap between the panel and the object (world units)
    [SerializeField] float moveSpeed = 12f;        // panel side-switching speed
    [SerializeField] float switchMargin = 0.2f;    // higher values make switching harder (prevents flickering)
    [SerializeField] float screenMargin = 20f;     // minimum distance from the edge of the screen (pixels)

    static readonly Vector2[] sides =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right
    };

    RectTransform panelRect;
    Vector2 defaultAnchoredPos;
    Transform target;
    Transform player;
    Vector2 targetExtents;       // half the object's width and height (world units)
    Vector2 targetCenterOffset;  // object's center relative to its position
    Vector2 offsetNow;
    int currentSide;

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
        defaultAnchoredPos = panelRect.anchoredPosition;
        panel.SetActive(false);
    }

    // If target is empty, the prompt stays at a fixed screen position (bottom center).
    // The offset parameter is no longer used; the distance is calculated automatically.
    public static void Show(string text, Transform target = null, Vector2 offset = default)
    {
        if (instance == null) return;

        instance.label.text = text;
        instance.target = target;
        instance.panel.SetActive(true);

        if (target != null)
        {
            instance.MeasureTarget();
            instance.FindPlayer();
            instance.currentSide = instance.BestSide();
            instance.offsetNow = instance.OffsetForSide(instance.currentSide);
        }

        instance.UpdatePosition();
    }

    public static void Hide()
    {
        if (instance == null) return;

        instance.target = null;
        instance.panel.SetActive(false);
    }

    void LateUpdate()
    {
        if (!panel.activeSelf) return;

        if (target != null)
        {
            FindPlayer();
            UpdateSide();

            Vector2 desired = OffsetForSide(currentSide);
            float t = 1f - Mathf.Exp(-moveSpeed * Time.deltaTime);
            offsetNow = Vector2.Lerp(offsetNow, desired, t);
        }

        UpdatePosition();
    }

    void FindPlayer()
    {
        if (player == null)
        {
            var p = FindAnyObjectByType<PlayerMovement>();
            if (p != null) player = p.transform;
        }
    }

    // Measure the object: use the sprite first, then the collider, or 1x1 if neither exists.
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

    // Score each side: the more it points "from the player to the object," the better.
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

        // Switch sides only when the new side is clearly better (prevents flickering diagonally).
        if (best != currentSide && bestScore > currentScore + switchMargin)
            currentSide = best;
    }

    // Half the panel size in world units (orthographic camera).
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
        if (target == null)
        {
            panelRect.anchoredPosition = defaultAnchoredPos;
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 world = target.position + (Vector3)offsetNow;
        Vector3 screen = cam.WorldToScreenPoint(world);

        // Keep the panel inside the screen.
        Vector2 half = new Vector2(
            panelRect.rect.width * panelRect.lossyScale.x,
            panelRect.rect.height * panelRect.lossyScale.y) * 0.5f;

        screen.x = Mathf.Clamp(screen.x, half.x + screenMargin, Screen.width - half.x - screenMargin);
        screen.y = Mathf.Clamp(screen.y, half.y + screenMargin, Screen.height - half.y - screenMargin);

        panelRect.position = screen;
    }
}