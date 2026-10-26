using UnityEngine;
using UnityEngine.InputSystem;

// A shelf or counter where the player takes an item for a shopping list (eggs, milk, bread, ...).
// Needs a Collider2D with Is Trigger ticked. Usable while a running errand still needs this item.
public class ShopItem : MonoBehaviour
{
    [Tooltip("Item id from the ItemData asset (e.g. \"Egg\").")]
    public string itemId = "Egg";
    public int amountPerTake = 1;

    [Tooltip("{0} = item name")]
    [GameText] public string promptFormat = "[E] Take {0}";

    [Tooltip("Ticked = only usable while a shopping list needs this item.")]
    public bool onlyWhenNeeded = true;

    [Tooltip("Shown when no list needs the item (e.g. \"Eggs\"). Empty = nothing.")]
    [GameText] public string idleLabel = "";

    bool playerNear;
    string shownText;
    bool shownAsLabel;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMovement>() != null)
            playerNear = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMovement>() != null)
        {
            playerNear = false;
            SetPrompt(null, false);
        }
    }

    void OnDisable()
    {
        playerNear = false;
        SetPrompt(null, false);
    }

    bool Needed
    {
        get
        {
            if (!onlyWhenNeeded) return true;
            QuestManager quest = QuestManager.Instance;
            return quest != null && quest.ShoppingNeed(itemId) > 0;
        }
    }

    void Update()
    {
        if (!playerNear) return;

        bool busy = InteractionState.IsInteracting;
        bool needed = Needed;

        if (busy) SetPrompt(null, false);
        else if (needed) SetPrompt(Loc.F(promptFormat, ItemName()), false);
        else SetPrompt(idleLabel, true);

        if (busy || !needed) return;

        var k = Keyboard.current;
        if (k == null || !k.eKey.wasPressedThisFrame) return;

        QuestManager quest = QuestManager.Instance;
        if (quest == null || quest.Inventory == null) return;

        quest.Inventory.AddItem(itemId, amountPerTake);
    }

    string ItemName()
    {
        QuestManager quest = QuestManager.Instance;
        ItemData data = quest != null && quest.Inventory != null ? quest.Inventory.FindItemData(itemId) : null;
        return data != null && !string.IsNullOrEmpty(data.displayName) ? Loc.T(data.displayName) : itemId;
    }

    // Only touches the shared prompt when our own state changes. The prompt text is already translated,
    // so it goes through Loc.T again harmlessly (no match = shown as is).
    void SetPrompt(string text, bool asLabel)
    {
        if (string.IsNullOrEmpty(text)) text = null;
        if (text == shownText && asLabel == shownAsLabel) return;

        bool wasShown = shownText != null;
        shownText = text;
        shownAsLabel = asLabel;

        if (text == null)
        {
            if (wasShown) InteractionPrompt.Hide();
        }
        else if (asLabel)
        {
            InteractionPrompt.ShowLabel(text, transform);
        }
        else
        {
            InteractionPrompt.Show(text, transform);
        }
    }
}
