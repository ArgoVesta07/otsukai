using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Side inventory list, opened and closed with a key (I by default) or a button.
// The open button sits where the panel appears: it hides while the panel is open, and the panel
// closes by itself after a few seconds. Put this on a root canvas (e.g. HudCanvas).
public class InventoryUI : MonoBehaviour
{
    static InventoryUI instance;

    [SerializeField] GameObject panel;           // the whole list, shown/hidden
    [SerializeField] Transform rowParent;        // rows are created under this
    [SerializeField] InventoryRow rowPrefab;
    [SerializeField] GameObject emptyText;       // optional "Empty" label
    [SerializeField] GameObject openButton;      // optional "[I] Bag" button, hidden while open
    [SerializeField] Key toggleKey = Key.I;
    [SerializeField] bool startOpen = false;
    [SerializeField] float autoCloseSeconds = 1.5f;   // 0 = stays open until toggled again

    readonly List<InventoryRow> rows = new List<InventoryRow>();
    InventoryManager inventory;
    bool dirty = true;
    float closeTimer;

    bool IsOpen => panel != null && panel.activeSelf;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        SetOpen(startOpen);
    }

    void OnDestroy()
    {
        if (inventory != null) inventory.Changed -= MarkDirty;
    }

    void Update()
    {
        ConnectInventory();

        var k = Keyboard.current;
        if (k != null && k[toggleKey].wasPressedThisFrame && !InteractionState.IsInteracting)
            Toggle();

        if (dirty && IsOpen)
        {
            Refresh();
            closeTimer = autoCloseSeconds;   // contents changed: keep it open a bit longer
        }

        // Auto-close after a delay
        if (IsOpen && autoCloseSeconds > 0f)
        {
            closeTimer -= Time.deltaTime;
            if (closeTimer <= 0f)
                SetOpen(false);
        }
    }

    // Hook this to the on-screen "[I] Bag" button's OnClick
    public void Toggle()
    {
        SetOpen(!IsOpen);
    }

    void SetOpen(bool open)
    {
        if (panel == null) return;

        panel.SetActive(open);

        // The button and the panel swap places
        if (openButton != null)
            openButton.SetActive(!open);

        if (open)
        {
            closeTimer = autoCloseSeconds;
            Refresh();
        }
    }

    void ConnectInventory()
    {
        if (inventory != null) return;

        QuestManager quest = QuestManager.Instance;
        if (quest == null || quest.Inventory == null) return;

        inventory = quest.Inventory;
        inventory.Changed += MarkDirty;
        dirty = true;
    }

    void MarkDirty()
    {
        dirty = true;
    }

    void Refresh()
    {
        dirty = false;

        foreach (InventoryRow row in rows)
        {
            if (row != null) Destroy(row.gameObject);
        }
        rows.Clear();

        int shown = 0;

        if (inventory != null && rowPrefab != null && rowParent != null)
        {
            foreach (var pair in inventory.All)
            {
                InventoryRow row = Instantiate(rowPrefab, rowParent);
                row.Setup(pair.Key, inventory.FindItemData(pair.Key), pair.Value);
                rows.Add(row);
                shown++;
            }
        }

        if (emptyText != null)
            emptyText.SetActive(shown == 0);
    }
}