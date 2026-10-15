using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class InventoryItemPreview : MonoBehaviour
{
    public TextMeshProUGUI ItemText;

    public void Setup(InventoryItem inventoryItem)
    {
        if (inventoryItem == null)
		{
            ItemText.text = GameplayText.Format("No Item Selected");
            return;
		}

        ItemText.text = GameplayText.Format($"{inventoryItem.ItemDefinition.Description}");

        if (inventoryItem is EquipableInventoryItem equipableInventoryItem)
		{
            string slot = equipableInventoryItem.EquipmentSlot switch
            {
                EquipmentSlot.MainHand => "\nMain hand",
                EquipmentSlot.TwoHand => "\nTwo-handed weapon",
                EquipmentSlot.OffHand => "\nOff hand",
                EquipmentSlot.Accessory => "\nAccessory",
                _ => ""
            };
            ItemText.text += GameplayText.Format(slot);
            List<string> effects = equipableInventoryItem.GetEquipmentStatModification().DescribeEffect();
            foreach(var effect in effects)
            {
                ItemText.text += GameplayText.Format("\n" + effect);
            }
        }
	}
}
