using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;

// Editor tools for the "Game" string table (menu: Otsukai > Localization).
//   Collect Game Texts : finds every [GameText] field and LocalizeText label in scenes, prefabs and
//                        data assets, adds new ones to the table (English = the text itself),
//                        and lists what still has no translation.
//   Export CSV / Import CSV : edit the whole table in a spreadsheet (columns: Key, en, ja).
public static class GameTextCollector
{
    const string TableFolder = "Assets/Localization/Tables";
    const string ScenesFolder = "Assets/Scenes";

    // ---------- Collect ----------

    [MenuItem("Otsukai/Localization/Collect Game Texts")]
    static void Collect()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Stop Play mode first.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // text -> first place it was found
        Dictionary<string, string> found = new Dictionary<string, string>();

        // Data assets (ErrandData, ItemData, ...)
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (IsOurs(asset)) Walk(asset, path, found, 0);
        }

        // Prefabs
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) ScanGameObject(prefab, path, found);
        }

        // Scenes (opened one by one in the background)
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { ScenesFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            foreach (GameObject root in scene.GetRootGameObjects())
                ScanGameObject(root, path, found);

            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
        }

        StringTableCollection collection = GetOrCreateCollection();
        if (collection == null) return;

        int added = 0;
        foreach (KeyValuePair<string, string> pair in found)
        {
            if (!collection.SharedData.Contains(pair.Key))
            {
                collection.SharedData.AddKey(pair.Key);
                added++;
            }

            // English = the key itself (only filled when empty, so English edits in the table are kept)
            foreach (StringTable table in collection.StringTables)
            {
                if (!IsEnglish(table)) continue;
                StringTableEntry entry = table.GetEntry(pair.Key);
                if (entry == null || string.IsNullOrEmpty(entry.Value))
                {
                    table.AddEntry(pair.Key, pair.Key);
                    EditorUtility.SetDirty(table);
                }
            }
        }

        EditorUtility.SetDirty(collection.SharedData);
        AssetDatabase.SaveAssets();

        Report(collection, found, added);
    }

    static void Report(StringTableCollection collection, Dictionary<string, string> found, int added)
    {
        StringBuilder log = new StringBuilder();
        log.AppendLine("Game texts: " + found.Count + " found, " + added + " new.");

        foreach (StringTable table in collection.StringTables)
        {
            if (IsEnglish(table)) continue;

            List<string> missing = new List<string>();
            foreach (KeyValuePair<string, string> pair in found)
            {
                StringTableEntry entry = table.GetEntry(pair.Key);
                if (entry == null || string.IsNullOrEmpty(entry.Value))
                    missing.Add("  \"" + OneLine(pair.Key) + "\"   (" + pair.Value + ")");
            }

            log.AppendLine(table.LocaleIdentifier.Code + ": " + missing.Count + " without a translation.");
            foreach (string line in missing) log.AppendLine(line);
        }

        // Keys that nothing uses anymore (e.g. the English text was changed). Not deleted automatically.
        List<string> unused = new List<string>();
        foreach (SharedTableData.SharedTableEntry entry in collection.SharedData.Entries)
        {
            if (!found.ContainsKey(entry.Key)) unused.Add("  \"" + OneLine(entry.Key) + "\"");
        }
        if (unused.Count > 0)
        {
            log.AppendLine(unused.Count + " key(s) are not used anymore (safe to delete in the table if you don't need them):");
            foreach (string line in unused) log.AppendLine(line);
        }

        Debug.Log(log.ToString());
    }

    static void ScanGameObject(GameObject root, string where, Dictionary<string, string> found)
    {
        foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;   // missing script

            if (mb is LocalizeText label)
                Add(label.SourceText, where + " > " + mb.name, found);
            else if (IsOurs(mb))
                Walk(mb, where + " > " + mb.name, found, 0);
        }
    }

    static bool IsOurs(UnityEngine.Object obj)
    {
        return obj != null && obj.GetType().Assembly.GetName().Name == "Assembly-CSharp";
    }

    // Goes through the serialized fields of an object and collects every [GameText] string
    static void Walk(object obj, string where, Dictionary<string, string> found, int depth)
    {
        if (obj == null || depth > 6) return;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (Type type = obj.GetType();
             type != null && type != typeof(MonoBehaviour) && type != typeof(ScriptableObject) && type != typeof(object);
             type = type.BaseType)
        {
            foreach (FieldInfo field in type.GetFields(flags))
            {
                if (field.IsNotSerialized) continue;
                if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null) continue;

                Type fieldType = field.FieldType;
                bool marked = field.GetCustomAttribute<GameTextAttribute>() != null;
                object value = field.GetValue(obj);

                if (fieldType == typeof(string))
                {
                    if (marked) Add((string)value, where, found);
                    continue;
                }

                Type element = ElementType(fieldType);

                if (element == typeof(string))
                {
                    if (marked && value is IEnumerable lines)
                        foreach (object line in lines) Add(line as string, where, found);
                    continue;
                }

                if (element != null)
                {
                    if (IsNestedData(element) && value is IEnumerable items)
                        foreach (object item in items) Walk(item, where, found, depth + 1);
                    continue;
                }

                if (IsNestedData(fieldType))
                    Walk(value, where, found, depth + 1);
            }
        }
    }

    static Type ElementType(Type type)
    {
        if (type.IsArray) return type.GetElementType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return type.GetGenericArguments()[0];
        return null;
    }

    // A [Serializable] class of our own (e.g. ErrandStep, DialogueEntry), not a reference to an asset
    static bool IsNestedData(Type type)
    {
        return type.IsClass && type.IsSerializable && type != typeof(string)
               && !typeof(UnityEngine.Object).IsAssignableFrom(type)
               && type.Assembly.GetName().Name == "Assembly-CSharp";
    }

    static void Add(string text, string where, Dictionary<string, string> found)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!found.ContainsKey(text)) found[text] = where;
    }

    // ---------- CSV ----------

    [MenuItem("Otsukai/Localization/Export CSV...")]
    static void ExportCsv()
    {
        StringTableCollection collection = GetOrCreateCollection();
        if (collection == null) return;

        string path = EditorUtility.SaveFilePanel("Export Game texts", "", "GameTexts.csv", "csv");
        if (string.IsNullOrEmpty(path)) return;

        List<StringTable> tables = new List<StringTable>(collection.StringTables);
        StringBuilder csv = new StringBuilder();

        csv.Append("Key");
        foreach (StringTable table in tables) csv.Append(",").Append(table.LocaleIdentifier.Code);
        csv.Append("\n");

        foreach (SharedTableData.SharedTableEntry shared in collection.SharedData.Entries)
        {
            csv.Append(Quote(shared.Key));
            foreach (StringTable table in tables)
            {
                StringTableEntry entry = table.GetEntry(shared.Key);
                csv.Append(",").Append(Quote(entry != null ? entry.Value : ""));
            }
            csv.Append("\n");
        }

        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));   // BOM so Excel reads Japanese
        Debug.Log("Exported " + collection.SharedData.Entries.Count + " texts to " + path);
    }

    [MenuItem("Otsukai/Localization/Import CSV...")]
    static void ImportCsv()
    {
        StringTableCollection collection = GetOrCreateCollection();
        if (collection == null) return;

        string path = EditorUtility.OpenFilePanel("Import Game texts", "", "csv");
        if (string.IsNullOrEmpty(path)) return;

        List<List<string>> rows = ParseCsv(File.ReadAllText(path, Encoding.UTF8));
        if (rows.Count < 2)
        {
            Debug.LogWarning("The CSV is empty.");
            return;
        }

        // Header: Key, en, ja, ... (a column is used when its name matches a locale code)
        List<string> header = rows[0];
        Dictionary<int, StringTable> columns = new Dictionary<int, StringTable>();
        for (int c = 1; c < header.Count; c++)
        {
            string code = header[c].Trim();
            foreach (StringTable table in collection.StringTables)
            {
                if (table.LocaleIdentifier.Code == code) columns[c] = table;
            }
        }

        if (columns.Count == 0)
        {
            Debug.LogWarning("No column matches a locale. The first row should be: Key,en,ja");
            return;
        }

        int changed = 0;
        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0])) continue;

            string key = Normalize(row[0]);
            if (!collection.SharedData.Contains(key)) collection.SharedData.AddKey(key);

            foreach (KeyValuePair<int, StringTable> column in columns)
            {
                if (column.Key >= row.Count) continue;
                string value = Normalize(row[column.Key]);
                if (string.IsNullOrEmpty(value)) continue;   // empty cells don't erase anything

                StringTableEntry entry = column.Value.GetEntry(key);
                if (entry != null && entry.Value == value) continue;

                column.Value.AddEntry(key, value);
                EditorUtility.SetDirty(column.Value);
                changed++;
            }
        }

        EditorUtility.SetDirty(collection.SharedData);
        AssetDatabase.SaveAssets();
        Debug.Log("Imported " + path + ": " + changed + " cell(s) updated.");
    }

    static List<List<string>> ParseCsv(string text)
    {
        List<List<string>> rows = new List<List<string>>();
        List<string> row = new List<string>();
        StringBuilder cell = new StringBuilder();
        bool quoted = false;

        if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];

            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else quoted = false;
                }
                else cell.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == ',') { row.Add(cell.ToString()); cell.Length = 0; }
            else if (ch == '\n' || ch == '\r')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString());
                cell.Length = 0;
                rows.Add(row);
                row = new List<string>();
            }
            else cell.Append(ch);
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        return rows;
    }

    static string Quote(string value)
    {
        if (value == null) return "";
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    static string Normalize(string value)
    {
        return value == null ? "" : value.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    // ---------- Helpers ----------

    static StringTableCollection GetOrCreateCollection()
    {
        StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(Loc.TableName);
        if (collection != null) return collection;

        if (LocalizationEditorSettings.GetLocales().Count == 0)
        {
            Debug.LogWarning("No locales yet. Add English and Japanese in Edit > Project Settings > Localization.");
            return null;
        }

        Directory.CreateDirectory(TableFolder);
        collection = LocalizationEditorSettings.CreateStringTableCollection(Loc.TableName, TableFolder);
        Debug.Log("Created the '" + Loc.TableName + "' string table in " + TableFolder);
        return collection;
    }

    static bool IsEnglish(StringTable table)
    {
        return table.LocaleIdentifier.Code.StartsWith("en");
    }

    static string OneLine(string text)
    {
        text = text.Replace("\n", " / ");
        return text.Length > 60 ? text.Substring(0, 60) + "..." : text;
    }
}
