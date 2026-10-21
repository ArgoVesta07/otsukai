using System;
using System.Collections;
using TMPro;
using UnityEngine;

// Full-screen title card between rounds ("Day 1 · Morning", "Perfect! 2/2").
// Locks the player while it's showing. Put this on a root canvas (e.g. HudCanvas).
public class PeriodBanner : MonoBehaviour
{
    static PeriodBanner instance;

    [SerializeField] CanvasGroup group;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text subtitleText;
    [SerializeField] float fadeSeconds = 0.3f;

    Coroutine running;

    public static bool IsShowing => instance != null && instance.running != null;

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

    // Shows the card for 'seconds', then calls 'done'. Without a banner in the scene, 'done' runs right away.
    public static void Show(string title, string subtitle, float seconds, Action done)
    {
        if (instance == null || instance.group == null)
        {
            Debug.Log("[Banner] " + title + " " + subtitle);
            done?.Invoke();
            return;
        }

        if (instance.running != null)
            instance.StopCoroutine(instance.running);

        instance.running = instance.StartCoroutine(instance.ShowRoutine(title, subtitle, seconds, done));
    }

    IEnumerator ShowRoutine(string title, string subtitle, float seconds, Action done)
    {
        InteractionState.IsInteracting = true;   // lock the player and pause timers

        if (titleText != null) titleText.text = title;
        if (subtitleText != null)
        {
            subtitleText.text = subtitle;
            subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
        }

        group.blocksRaycasts = true;
        yield return Fade(1f);

        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return Fade(0f);
        group.blocksRaycasts = false;

        InteractionState.IsInteracting = false;
        running = null;
        done?.Invoke();
    }

    IEnumerator Fade(float target)
    {
        float start = group.alpha;
        float t = 0f;

        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, t / fadeSeconds);
            yield return null;
        }

        group.alpha = target;
    }
}
