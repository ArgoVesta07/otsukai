using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Saves and loads the game to one JSON file. Call Save() from checkpoints (SavePoint),
// and LoadGame() from the main menu's Continue button.
public static class SaveSystem
{
    public const int CurrentVersion = 1;
    const string FileName = "save.json";

    static SaveData pendingLoad;   // waiting to be applied once the game scene has loaded

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
    public static bool HasSave => File.Exists(FilePath);

    // Runs once when Play starts, so it also works with domain reload turned off
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        pendingLoad = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // ---------- Save ----------

    public static bool Save()
    {
        SaveData data = Collect();

        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            Debug.Log("Game saved: " + FilePath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Save failed: " + e.Message);
            return false;
        }
    }

    static SaveData Collect()
    {
        SaveData data = new SaveData
        {
            version = CurrentVersion,
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            sceneName = SceneManager.GetActiveScene().name
        };

        GameObject player = GameObject.Find("player");
        if (player != null)
            data.playerPosition = player.transform.position;

        QuestManager quest = QuestManager.Instance;
        if (quest != null)
        {
            quest.WriteTo(data);

            if (quest.Inventory != null)
            {
                foreach (var pair in quest.Inventory.All)
                    data.items.Add(new SavedPair(pair.Key, pair.Value));
            }
        }

        foreach (var pair in GameFlags.All)
            data.flags.Add(new SavedPair(pair.Key, pair.Value));

        return data;
    }

    // ---------- Load ----------

    // Reads the save, opens the game scene, then restores everything (see OnSceneLoaded).
    // gameSceneName is the scene that holds the managers (QuestManager, HUD, ...).
    public static bool LoadGame(string gameSceneName)
    {
        SaveData data = ReadFile();
        if (data == null) return false;

        pendingLoad = data;
        SceneManager.LoadScene(gameSceneName);
        return true;
    }

    static SaveData ReadFile()
    {
        if (!HasSave)
        {
            Debug.LogWarning("No save file found.");
            return null;
        }

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));

            if (data != null && data.version > CurrentVersion)
                Debug.LogWarning("This save was made by a newer version of the game.");

            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Load failed: " + e.Message);
            return null;
        }
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pendingLoad == null) return;

        SaveData data = pendingLoad;
        pendingLoad = null;

        Apply(data);

        // The save was made in another scene (e.g. House): go there with the loading screen
        if (!string.IsNullOrEmpty(data.sceneName) && data.sceneName != scene.name && SceneTransition.Instance != null)
        {
            SceneTransition.Instance.GoToScene(data.sceneName, data.playerPosition);
        }
        else
        {
            GameObject player = GameObject.Find("player");
            if (player != null)
                player.transform.position = data.playerPosition;
        }

        Debug.Log("Game loaded (saved " + data.savedAt + ").");
    }

    static void Apply(SaveData data)
    {
        InteractionState.IsInteracting = false;

        GameFlags.Clear();
        foreach (SavedPair pair in data.flags)
            GameFlags.Set(pair.key, pair.value);

        QuestManager quest = QuestManager.Instance;
        if (quest == null)
        {
            Debug.LogWarning("QuestManager was not found while loading. Is it in the game scene?");
            return;
        }

        quest.ReadFrom(data);

        if (quest.Inventory != null)
        {
            quest.Inventory.Clear();
            foreach (SavedPair pair in data.items)
                quest.Inventory.SetItem(pair.key, pair.value);
        }
    }

    // ---------- New game / delete ----------

    // Clears progress kept in memory (for "New Game"). Does not delete the save file.
    public static void ResetProgress()
    {
        pendingLoad = null;
        InteractionState.IsInteracting = false;
        GameFlags.Clear();

        QuestManager quest = QuestManager.Instance;
        if (quest != null)
        {
            quest.ResetState();
            if (quest.Inventory != null) quest.Inventory.Clear();
        }
    }

    public static void DeleteSave()
    {
        if (HasSave)
        {
            File.Delete(FilePath);
            Debug.Log("Save deleted.");
        }
    }
}
