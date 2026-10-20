using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    private Dictionary<string, int> items = new Dictionary<string, int>();

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
    }

    public void Clear()
    {
        items.Clear();
    }
}