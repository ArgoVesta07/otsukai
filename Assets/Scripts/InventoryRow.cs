using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One line in the inventory list: icon, name, count. Lives on the row prefab.
public class InventoryRow : MonoBehaviour
{
    [SerializeField] Image icon;
    [SerializeField] TMP_Text iconLetter;   // shown when the item has no icon sprite
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text countText;

    public void Setup(string itemId, ItemData data, int count)
    {
        string displayName = data != null && !string.IsNullOrEmpty(data.displayName)
            ? data.displayName
            : itemId;

        bool hasIcon = data != null && data.icon != null;

        if (icon != null)
        {
            icon.sprite = hasIcon ? data.icon : null;
            icon.enabled = hasIcon;
        }

        if (iconLetter != null)
        {
            iconLetter.gameObject.SetActive(!hasIcon);
            iconLetter.text = displayName.Length > 0 ? displayName.Substring(0, 1).ToUpper() : "?";
        }

        if (nameText != null) nameText.text = displayName;
        if (countText != null) countText.text = "x" + count;
    }
}
