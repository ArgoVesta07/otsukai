using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// The shopping memo: a note-paper panel (Tab) listing what the current errand needs.
// Shopping-list items show how many are in the bag; other steps show done / not done.
// The look (paper image, font, size) is made in the scene. Put this on a root canvas (e.g. HudCanvas).
public class ShoppingMemo : MonoBehaviour
{
    static ShoppingMemo instance;

    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text bodyText;
    [SerializeField] Key toggleKey = Key.Tab;
    [Tooltip("Ticked = the memo follows the otsukai first, even when another errand is tracked.")]
    [SerializeField] bool preferOtsukai = true;
    [Tooltip("Ticked = also list the other steps (e.g. \"Bring it all to Mom\"). Off = only the shopping list.")]
    [SerializeField] bool showOtherSteps = false;

    [Header("Text")]
    [Tooltip("{0} = who asked")]
    [GameText] [SerializeField] string titleFormat = "Memo from {0}";
    [GameText] [SerializeField] string titleNoGiver = "Memo";
    [GameText] [SerializeField] string emptyText = "Nothing to do right now.";
    [Tooltip("{0} = mark, {1} = item name, {2} = in the bag, {3} = needed")]
    [SerializeField] string itemLineFormat = "{0} {1}  {2}/{3}";
    [Tooltip("{0} = mark, {1} = step text")]
    [SerializeField] string stepLineFormat = "{0} {1}";
    [SerializeField] string doneMark = "■";   // filled square
    [SerializeField] string todoMark = "□";   // empty square

    string shown;

    public static bool IsOpen => instance != null && instance.panel != null && instance.panel.activeSelf;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        if (panel != null) panel.SetActive(false);
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k != null && k[toggleKey].wasPressedThisFrame && !InteractionState.IsInteracting)
            Toggle();

        if (IsOpen) Refresh();
    }

    // Can also be hooked to a HUD button
    public void Toggle()
    {
        if (panel == null) return;
        panel.SetActive(!panel.activeSelf);
        shown = null;
        if (panel.activeSelf) Refresh();
    }

    void Refresh()
    {
        QuestManager quest = QuestManager.Instance;
        ActiveErrand errand = PickErrand(quest);

        string title;
        string body;

        if (errand == null)
        {
            title = Loc.T(titleNoGiver);
            body = Loc.T(emptyText);
        }
        else
        {
            title = string.IsNullOrEmpty(errand.giverName)
                ? Loc.T(titleNoGiver)
                : Loc.F(titleFormat, Loc.T(errand.giverName));
            body = BuildLines(quest, errand);
        }

        string all = title + "\n" + body;
        if (all == shown) return;   // nothing changed
        shown = all;

        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = body;
    }

    ActiveErrand PickErrand(QuestManager quest)
    {
        if (quest == null || quest.Active.Count == 0) return null;

        if (preferOtsukai)
        {
            foreach (ActiveErrand e in quest.Active)
            {
                if (e.IsOtsukai) return e;
            }
        }

        return quest.Tracked;
    }

    string BuildLines(QuestManager quest, ActiveErrand errand)
    {
        StringBuilder lines = new StringBuilder();
        ErrandStep[] steps = errand.data.steps;
        if (steps == null) return "";

        for (int i = 0; i < steps.Length; i++)
        {
            ErrandStep step = steps[i];
            if (step == null) continue;
            bool done = i < errand.stepIndex;

            if (step.IsShopping)
            {
                foreach (ItemAmount item in step.shoppingList)
                {
                    if (item == null || string.IsNullOrEmpty(item.itemId)) continue;

                    int have = done ? item.amount : Mathf.Min(item.amount, Count(quest, item.itemId));
                    string mark = have >= item.amount ? doneMark : todoMark;
                    Append(lines, string.Format(itemLineFormat, mark, ItemName(quest, item.itemId), have, item.amount));
                }
            }
            else if (showOtherSteps)
            {
                string text = errand.Fill(Loc.T(step.shortText));
                Append(lines, string.Format(stepLineFormat, done ? doneMark : todoMark, text));
            }
        }

        return lines.Length > 0 ? lines.ToString() : Loc.T(emptyText);
    }

    static void Append(StringBuilder lines, string line)
    {
        if (lines.Length > 0) lines.Append('\n');
        lines.Append(line);
    }

    static int Count(QuestManager quest, string itemId)
    {
        return quest.Inventory != null ? quest.Inventory.GetItemCount(itemId) : 0;
    }

    static string ItemName(QuestManager quest, string itemId)
    {
        ItemData data = quest.Inventory != null ? quest.Inventory.FindItemData(itemId) : null;
        return data != null && !string.IsNullOrEmpty(data.displayName) ? Loc.T(data.displayName) : itemId;
    }
}
