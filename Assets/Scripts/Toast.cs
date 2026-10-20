using TMPro;
using UnityEngine;

// Short pop-up message on screen ("Saved", "Got Egg", ...). Call Toast.Show("text") from any script.
// Put this on a root canvas (e.g. HudCanvas). Position, size and look live on the UI objects.
public class Toast : MonoBehaviour
{
    static Toast instance;

    [SerializeField] CanvasGroup group;      // on the toast panel, controls the fade
    [SerializeField] TMP_Text label;
    [SerializeField] float showSeconds = 2f;
    [SerializeField] float fadeSeconds = 0.25f;

    float timeLeft;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }
    }

    // Shows a message. seconds <= 0 uses the default duration from the Inspector.
    public static void Show(string text, float seconds = 0f)
    {
        if (instance == null)
        {
            Debug.Log("[Toast] " + text);
            return;
        }

        instance.label.text = text;
        instance.timeLeft = seconds > 0f ? seconds : instance.showSeconds;
    }

    void Update()
    {
        if (group == null) return;

        // Unscaled time so it still works if the game is paused later
        float dt = Time.unscaledDeltaTime;
        if (timeLeft > 0f) timeLeft -= dt;

        float target = timeLeft > 0f ? 1f : 0f;
        float speed = fadeSeconds > 0f ? dt / fadeSeconds : 1f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, speed);
    }
}