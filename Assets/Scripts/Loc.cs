using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

// Marks a string field (or string array) as text the player sees.
// "Otsukai > Localization > Collect Game Texts" finds every marked field and adds it to the "Game" table.
[AttributeUsage(AttributeTargets.Field)]
public class GameTextAttribute : Attribute { }

// Translates game text. The English text itself is the key in the "Game" string table.
//   Loc.T("Buy eggs")            -> "たまごを買う" when Japanese is selected
//   Loc.F("Day {0}", 2)          -> "2日目"
// Text that isn't in the table is shown as it is (English), so nothing breaks.
// Rule for scripts: wrap text from a [GameText] field with Loc.T / Loc.F right before it is shown.
public static class Loc
{
    public const string TableName = "Game";

    static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
    static readonly HashSet<string> warned = new HashSet<string>();
    static Locale cachedLocale;
    static StringTable table;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        cache.Clear();
        warned.Clear();
        cachedLocale = null;
        table = null;
    }

    public static string T(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        Locale locale = LocalizationSettings.SelectedLocale;
        if (locale == null) return text;

        if (locale != cachedLocale)
        {
            cachedLocale = locale;
            cache.Clear();
            table = LoadTable();
        }

        if (cache.TryGetValue(text, out string result)) return result;

        result = text;
        StringTableEntry entry = table != null ? table.GetEntry(text) : null;
        if (entry != null && !string.IsNullOrEmpty(entry.Value))
            result = entry.Value;
        else
            WarnMissing(text, locale);

        cache[text] = result;
        return result;
    }

    public static string F(string format, params object[] args)
    {
        if (string.IsNullOrEmpty(format)) return format;

        string text = T(format);

        try
        {
            return string.Format(text, args);
        }
        catch (FormatException)
        {
            // A {n} marker that doesn't exist here (e.g. {4} when only 4 values are given).
            // Show the text without filling it in, instead of breaking the game.
            if (warned.Add("format:" + format))
                Debug.LogWarning("Loc: \"" + text + "\" uses a {n} marker that isn't available here. Check the numbers in the brackets.");
            return text;
        }
    }

    // Translates every line of an array (dialogue). Returns a new array.
    public static string[] T(string[] lines)
    {
        if (lines == null) return null;

        string[] result = new string[lines.Length];
        for (int i = 0; i < lines.Length; i++)
            result[i] = T(lines[i]);
        return result;
    }

    static StringTable LoadTable()
    {
        try
        {
            return LocalizationSettings.StringDatabase.GetTable(TableName);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Loc: couldn't load the '" + TableName + "' table. " + e.Message);
            return null;
        }
    }

    static void WarnMissing(string text, Locale locale)
    {
        if (!Debug.isDebugBuild) return;
        if (locale.Identifier.Code.StartsWith("en")) return;   // English falls back to the key itself
        if (!warned.Add(text)) return;

        Debug.LogWarning("No " + locale.Identifier.Code + " text for: \"" + text + "\"  (run Otsukai > Localization > Collect Game Texts, then fill it in the Game table)");
    }
}
