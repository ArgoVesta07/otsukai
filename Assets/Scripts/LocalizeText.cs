using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

// Put this on a fixed label (Paused, Resume, Yes, No, ...). The English text typed in the
// TMP component is used as the key in the "Game" table, and the label follows the language.
// Safe on text that a script changes: once a script writes its own text, this stays out of the way.
[RequireComponent(typeof(TMP_Text))]
public class LocalizeText : MonoBehaviour
{
    [Tooltip("Empty = use the text typed in the TMP component.")]
    [SerializeField] string key;

    TMP_Text label;
    string source;
    string lastApplied;

    // The English key, also read by the Collect Game Texts tool
    public string SourceText
    {
        get
        {
            if (!string.IsNullOrEmpty(key)) return key;
            if (source != null) return source;
            TMP_Text t = GetComponent<TMP_Text>();
            return t != null ? t.text : "";
        }
    }

    void Awake()
    {
        label = GetComponent<TMP_Text>();
        source = SourceText;
        lastApplied = label.text;
    }

    void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        Apply();
    }

    void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    void OnLocaleChanged(Locale locale)
    {
        Apply();
    }

    void Apply()
    {
        if (label == null || string.IsNullOrEmpty(source)) return;

        // A script has written something else here: leave it alone
        if (label.text != source && label.text != lastApplied) return;

        lastApplied = Loc.T(source);
        label.text = lastApplied;
    }
}
