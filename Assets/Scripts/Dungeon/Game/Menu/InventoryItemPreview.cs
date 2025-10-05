using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class InventoryItemPreview : MonoBehaviour
{
    public TextMeshProUGUI ItemText;

    public void Setup(InventoryItem inventoryItem)
    {
        ItemText.text = $"{inventoryItem.ItemDefinition.Description}";

        if (inventoryItem is EquipableInventoryItem equipableInventoryItem)
		{
            string slot = equipableInventoryItem.EquipmentSlot switch
            {
                EquipmentSlot.MainHand => " (Main Hand)",
                EquipmentSlot.TwoHand => " (Two-Handed)",
                EquipmentSlot.OffHand => " (Off-Hand)",
                EquipmentSlot.Accessory => " (Accessory)",
                _ => ""
            };
            ItemText.text += slot;
            List<string> effects = equipableInventoryItem.GetEquipmentStatModification().DescribeEffect();
            foreach(var effect in effects)
            {
                ItemText.text += "\n" + effect;
            }
        }
	}
}
