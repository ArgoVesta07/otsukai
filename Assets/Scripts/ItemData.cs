using UnityEngine;

// One item type (egg, coins, shopping list, ...), made as an asset:
// right-click in the Project window > Create > Otsukai > Item.
// Errands and the inventory refer to items by Item Id.
[CreateAssetMenu(menuName = "Otsukai/Item", fileName = "NewItem")]
public class ItemData : ScriptableObject
{
    [Tooltip("Unique id, e.g. 'Egg'. Must match the item names used in ErrandData steps.")]
    public string itemId = "NewItem";

    [Tooltip("Name shown in the inventory.")]
    public string displayName = "New item";

    [Tooltip("Optional. When empty, the first letter of the name is shown instead.")]
    public Sprite icon;

    [TextArea(2, 4)] public string description;
}
