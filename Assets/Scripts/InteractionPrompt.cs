using UnityEngine;
using TMPro;

[DefaultExecutionOrder(100)]   // jalan setelah CameraFollow, biar nggak getar
public class InteractionPrompt : MonoBehaviour
{
    static InteractionPrompt instance;

    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text label;

    [Header("Posisi")]
    [SerializeField] float gap = 0.15f;            // celah antara panel dan objek (unit dunia)
    [SerializeField] float moveSpeed = 12f;        // kecepatan panel pindah sisi
    [SerializeField] float switchMargin = 0.2f;    // makin besar, makin susah pindah (anti kedip)
    [SerializeField] float screenMargin = 20f;     // jarak minimal dari tepi layar (pixel)

    static readonly Vector2[] sides =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right
    };

    RectTransform panelRect;
    Vector2 defaultAnchoredPos;
    Transform target;
    Transform player;
    Vector2 targetExtents;       // setengah lebar dan tinggi objek (unit dunia)
    Vector2 targetCenterOffset;  // titik tengah objek relatif ke posisinya
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

    // target kosong = prompt tetap di posisi layar (bawah tengah).
    // Parameter offset sudah nggak dipakai, jaraknya dihitung otomatis.
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

    // ukur objek: pakai sprite dulu, kalau nggak ada pakai collider, kalau nggak ada pakai 1x1
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

    // skor tiap sisi: makin searah "dari player ke objek", makin bagus
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

        // pindah sisi cuma kalau sisi baru jelas lebih bagus (hindari kedip di diagonal)
        if (best != currentSide && bestScore > currentScore + switchMargin)
            currentSide = best;
    }

    // setengah ukuran panel dalam unit dunia (kamera orthographic)
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

        // jaga panel tetap di dalam layar
        Vector2 half = new Vector2(
            panelRect.rect.width * panelRect.lossyScale.x,
            panelRect.rect.height * panelRect.lossyScale.y) * 0.5f;

        screen.x = Mathf.Clamp(screen.x, half.x + screenMargin, Screen.width - half.x - screenMargin);
        screen.y = Mathf.Clamp(screen.y, half.y + screenMargin, Screen.height - half.y - screenMargin);

        panelRect.position = screen;
    }
}