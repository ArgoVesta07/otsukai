using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    [Tooltip("Every ItemData asset in the game, so the inventory can show names and icons.")]
    [SerializeField] ItemData[] itemLibrary;

    private Dictionary<string, int> items = new Dictionary<string, int>();

    public ItemData[] ItemLibrary => itemLibrary;

    // Fired whenever the contents change (used by the inventory UI)
    public event Action Changed;

    public void AddItem(string itemName, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemName) || amount <= 0)
            return;

        if (items.ContainsKey(itemName))
        {
            items[itemName] += amount;
        }
        else
        {
            items.Add(itemName, amount);
        }

        Debug.Log($"Got {itemName} x{amount}. Total: {GetItemCount(itemName)}");
        Changed?.Invoke();
    }

    public bool RemoveItem(string itemName, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemName) || amount <= 0)
            return false;

        if (!items.ContainsKey(itemName) || items[itemName] < amount)
            return false;

        items[itemName] -= amount;

        if (items[itemName] == 0)
        {
            items.Remove(itemName);
        }

        Debug.Log($"Removed {itemName} x{amount}.");
        Changed?.Invoke();
        return true;
    }

    public int GetItemCount(string itemName)
    {
        if (items.TryGetValue(itemName, out int amount))
        {
            return amount;
        }

        return 0;
    }

    public bool HasItem(string itemName, int amount = 1)
    {
        return GetItemCount(itemName) >= amount;
    }

    // Looks up the ItemData asset for an item id (null if it isn't in the Item Library)
    public ItemData FindItemData(string itemId)
    {
        if (itemLibrary == null) return null;

        foreach (ItemData data in itemLibrary)
        {
            if (data != null && data.itemId == itemId) return data;
        }

        return null;
    }

    // ---------- Save / load ----------

    // Read-only view of everything the player carries
    public IReadOnlyDictionary<string, int> All => items;

    // Sets an item count directly, without a log message (used when loading)
    public void SetItem(string itemName, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemName)) return;

        if (amount <= 0)
            items.Remove(itemName);
        else
            items[itemName] = amount;

        Changed?.Invoke();
    }

    public void Clear()
    {
        items.Clear();
        Changed?.Invoke();
    }
}