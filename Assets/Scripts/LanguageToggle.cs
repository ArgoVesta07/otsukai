using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

// EN | JP switch. Hook the two buttons' OnClick to ChooseEnglish / ChooseJapanese.
// The chosen button is highlighted. The language itself is changed through the Localization package.
public class LanguageToggle : MonoBehaviour
{
    [SerializeField] Image englishBackground;
    [SerializeField] Image japaneseBackground;
    [SerializeField] Color selectedColor = new Color(0.95f, 0.79f, 0.30f);   // yellow
    [SerializeField] Color normalColor = new Color(0f, 0f, 0f, 0.45f);

    void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        Refresh();
    }

    void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    public void ChooseEnglish()
    {
        LanguageSetting.Set(GameLanguage.English);
    }

    public void ChooseJapanese()
    {
        LanguageSetting.Set(GameLanguage.Japanese);
    }

    void OnLocaleChanged(Locale locale)
    {
        Refresh();
    }

    void Refresh()
    {
        bool english = LanguageSetting.Current == GameLanguage.English;

        if (englishBackground != null) englishBackground.color = english ? selectedColor : normalColor;
        if (japaneseBackground != null) japaneseBackground.color = english ? normalColor : selectedColor;
    }
}
