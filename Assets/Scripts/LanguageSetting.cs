using System;
using UnityEngine;

public enum GameLanguage
{
    English,
    Japanese
}

// Remembers the chosen language (kept between sessions).
// Text that changes with the language will listen to Changed later.
public static class LanguageSetting
{
    const string Key = "Otsukai_Language";

    public static event Action Changed;

    public static GameLanguage Current
    {
        get { return (GameLanguage)PlayerPrefs.GetInt(Key, (int)GameLanguage.English); }
    }

    public static void Set(GameLanguage language)
    {
        if (language == Current) return;

        PlayerPrefs.SetInt(Key, (int)language);
        PlayerPrefs.Save();
        Debug.Log("Language set to " + language);
        Changed?.Invoke();
    }
}
