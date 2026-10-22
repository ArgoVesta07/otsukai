using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public enum GameLanguage
{
    English,
    Japanese
}

// Small helper over the Unity Localization package.
// The package itself holds the current language (LocalizationSettings.SelectedLocale) and remembers it
// between sessions through the "Player Pref Locale Selector" set in the Localization Settings.
// This class only translates our two languages to the package's locale codes.
public static class LanguageSetting
{
    static readonly string[] localeCodes = { "en", "ja" };   // same order as GameLanguage

    public static GameLanguage Current
    {
        get
        {
            Locale locale = LocalizationSettings.SelectedLocale;
            if (locale != null && locale.Identifier.Code.StartsWith("ja"))
                return GameLanguage.Japanese;
            return GameLanguage.English;
        }
    }

    public static void Set(GameLanguage language)
    {
        string code = localeCodes[(int)language];
        Locale locale = LocalizationSettings.AvailableLocales.GetLocale(code);

        if (locale == null)
        {
            Debug.LogWarning("Locale '" + code + "' is not set up. Add it in Edit > Project Settings > Localization.");
            return;
        }

        LocalizationSettings.SelectedLocale = locale;   // every localized text updates from this
        Debug.Log("Language set to " + language);
    }
}
