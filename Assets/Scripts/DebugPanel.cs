using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Developer-only panel for testing (P). Jump to any day/period, change the timer,
// finish or fail errands, give items, toggle flags and incidents, save or delete the save.
// Drawn with Unity's built-in debug GUI, so it needs no Canvas. Put it on any object in the game scene.
// It only works in the Unity Editor and in Development Builds, never in the final game.
public class DebugPanel : MonoBehaviour
{
    static DebugPanel instance;

    [SerializeField] Key toggleKey = Key.P;
    [Tooltip("Bigger = larger panel and text.")]
    [SerializeField] float scale = 1.5f;
    [SerializeField] string gameSceneName = "MainScene";

    static readonly string[] tabs = { "Time", "Errands", "Items", "Flags", "Misc" };

    bool open;
    int tab;
    Vector2 scroll;
    Rect window = new Rect(10, 10, 380, 460);
    string newFlag = "";

    static bool Allowed => Application.isEditor || Debug.isDebugBuild;

    void Awake()
    {
        if (!Allowed)
        {
            Destroy(this);   // final game: remove just this component
            return;
        }

        if (instance != null && instance != this)
        {
            Destroy(gameObject);   // duplicate from a reloaded scene
            return;
        }

        instance = this;
        if (transform.parent == null) DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k == null || !k[toggleKey].wasPressedThisFrame) return;

        // Don't close the panel while typing in its text field (e.g. a flag name with a "p")
        if (open && GUIUtility.keyboardControl != 0) return;

        open = !open;
    }

    void OnGUI()
    {
        if (!open) return;

        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        window = GUILayout.Window(91827, window, DrawWindow, "Debug (" + toggleKey + ")");
        GUI.matrix = old;
    }

    void DrawWindow(int id)
    {
        tab = GUILayout.Toolbar(tab, tabs);
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(380));

        switch (tab)
        {
            case 0: DrawTime(); break;
            case 1: DrawErrands(); break;
            case 2: DrawItems(); break;
            case 3: DrawFlags(); break;
            case 4: DrawMisc(); break;
        }

        GUILayout.EndScrollView();
        GUI.DragWindow();
    }

    // ---------- Tabs ----------

    void DrawTime()
    {
        TimeManager time = TimeManager.Instance;
        if (time == null || !time.HasStory)
        {
            GUILayout.Label("No TimeManager or no Story assigned.");
            return;
        }

        string state = time.StoryFinished ? "Story finished"
                     : time.OnBreak ? "On break"
                     : time.Running ? "Running" : "Starting / banner";
        GUILayout.Label(time.CurrentLabel + "   " + state);
        GUILayout.Label("Time left: " + Mathf.CeilToInt(time.TimeLeft) + " s");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+30 s")) time.DebugSetTimeLeft(time.TimeLeft + 30f);
        if (GUILayout.Button("Set 5 s")) time.DebugSetTimeLeft(5f);
        if (GUILayout.Button("End round")) time.DebugEndRoundNow();
        GUILayout.EndHorizontal();

        time.DebugFreezeTimer = GUILayout.Toggle(time.DebugFreezeTimer, " Freeze round timer");

        GUILayout.Space(8);
        GUILayout.Label("Jump to:");

        StoryData story = time.Story;
        for (int d = 0; d < story.days.Length; d++)
        {
            DayData day = story.days[d];
            if (day == null || day.periods == null) continue;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Day " + (d + 1), GUILayout.Width(50));
            for (int p = 0; p < day.periods.Length; p++)
            {
                if (GUILayout.Button(day.periods[p].period.ToString()))
                    time.DebugJumpTo(d, p);
            }
            GUILayout.EndHorizontal();
        }
    }

    void DrawErrands()
    {
        QuestManager quest = QuestManager.Instance;
        if (quest == null)
        {
            GUILayout.Label("No QuestManager.");
            return;
        }

        GUILayout.Label("Active:");
        if (quest.Active.Count == 0) GUILayout.Label("  (none)");

        // Copy, because the buttons can change the list
        List<ActiveErrand> running = new List<ActiveErrand>(quest.Active);
        foreach (ActiveErrand errand in running)
        {
            string step = errand.CurrentStep != null ? errand.CurrentStep.shortText : "?";
            GUILayout.Label("  " + errand.data.errandId + "  step " + errand.stepIndex + ": " + step);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Next step")) quest.DebugCompleteStep(errand);
            if (GUILayout.Button("Complete")) quest.DebugCompleteErrand(errand);
            if (GUILayout.Button("Fail")) quest.ForceFail(errand.data);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(8);
        GUILayout.Label("All errands (Errand Library):");

        if (quest.ErrandLibrary == null) return;
        foreach (ErrandData data in quest.ErrandLibrary)
        {
            if (data == null) continue;

            GUILayout.BeginHorizontal();
            GUILayout.Label(data.errandId + "  [" + quest.GetState(data.errandId) + "]");
            if (GUILayout.Button("Start", GUILayout.Width(70))) quest.DebugForceStart(data);
            GUILayout.EndHorizontal();
        }
    }

    void DrawItems()
    {
        QuestManager quest = QuestManager.Instance;
        InventoryManager inv = quest != null ? quest.Inventory : null;
        if (inv == null)
        {
            GUILayout.Label("No InventoryManager.");
            return;
        }

        GUILayout.Label("Carrying:");
        if (inv.All.Count == 0) GUILayout.Label("  (empty)");

        List<string> ids = new List<string>(inv.All.Keys);
        foreach (string id in ids)
            DrawItemRow(inv, id);

        GUILayout.Space(8);
        GUILayout.Label("Add from Item Library:");

        if (inv.ItemLibrary == null) return;
        foreach (ItemData data in inv.ItemLibrary)
        {
            if (data == null) continue;

            GUILayout.BeginHorizontal();
            GUILayout.Label(data.itemId);
            if (GUILayout.Button("+1", GUILayout.Width(50))) inv.AddItem(data.itemId, 1);
            GUILayout.EndHorizontal();
        }
    }

    void DrawItemRow(InventoryManager inv, string id)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(id + "  x" + inv.GetItemCount(id));
        if (GUILayout.Button("+1", GUILayout.Width(50))) inv.AddItem(id, 1);
        if (GUILayout.Button("-1", GUILayout.Width(50))) inv.RemoveItem(id, 1);
        GUILayout.EndHorizontal();
    }

    void DrawFlags()
    {
        if (GameFlags.All.Count == 0) GUILayout.Label("(no flags set)");

        List<string> names = new List<string>(GameFlags.All.Keys);
        names.Sort();
        foreach (string name in names)
        {
            int value = GameFlags.Get(name);

            GUILayout.BeginHorizontal();
            GUILayout.Label(name + " = " + value);
            if (GUILayout.Button("+1", GUILayout.Width(40))) GameFlags.Set(name, value + 1);
            if (GUILayout.Button("0", GUILayout.Width(40))) GameFlags.Set(name, 0);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        newFlag = GUILayout.TextField(newFlag);
        if (GUILayout.Button("Set to 1", GUILayout.Width(80)) && newFlag.Trim().Length > 0)
        {
            GameFlags.Set(newFlag.Trim(), 1);
            newFlag = "";
        }
        GUILayout.EndHorizontal();
    }

    void DrawMisc()
    {
        GUILayout.Label("Incidents:");
        bool test = IncidentManager.IsActive("Test");
        if (GUILayout.Button(test ? "End test incident" : "Start test incident"))
        {
            if (test) IncidentManager.End("Test");
            else IncidentManager.Begin("Test");
        }

        GUILayout.Space(8);
        GUILayout.Label("Save file: " + (SaveSystem.HasSave ? "exists" : "none"));

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save now")) Toast.Show(SaveSystem.Save() ? "Saved (debug)" : "Save failed");
        if (GUILayout.Button("Load save")) SaveSystem.LoadGame(gameSceneName);
        if (GUILayout.Button("Delete save")) SaveSystem.DeleteSave();
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.Label("Player locked: " + InteractionState.IsInteracting);
        if (GUILayout.Button("Unlock player")) InteractionState.IsInteracting = false;
    }
}