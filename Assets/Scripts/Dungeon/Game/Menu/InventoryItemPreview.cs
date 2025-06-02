using TMPro;
using UnityEngine;

public class InventoryItemPreview : MonoBehaviour
{
    public TextMeshProUGUI ItemText;

    public void Setup(InventoryItem inventoryItem)
    {
        ItemText.text = $"{inventoryItem.ItemDefinition.Description}";
    }
}
