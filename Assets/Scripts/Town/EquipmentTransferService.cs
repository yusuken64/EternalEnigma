using System.Collections.Generic;
using System.Linq;

/// <summary>Transfers the actual item instances; names are never transaction keys.</summary>
public static class EquipmentTransferService
{
    public static bool Toggle(Equipment slots, List<InventoryItem> bag, InventoryItem item,
        ClassDefinition primary, ClassDefinition secondary, out string reason)
    {
        reason = "This equipment is no longer available.";
        if (slots == null || bag == null || item is not EquipableInventoryItem equipment || equipment.EquipmentItemDefinition == null) return false;
        if (slots.IsEquipped(item))
        {
            slots.UnEquip(equipment);
            bag.Add(item);
        }
        else
        {
            if (!bag.Contains(item)) return false;
            if (!HeroClass.AllowsItem(primary, secondary, equipment))
            {
                reason = HeroClass.EquipmentRestriction(primary, secondary, equipment);
                return false;
            }
            var previous = slots.GetEquippedItems().ToArray();
            bag.Remove(item);
            slots.Equip(equipment);
            foreach (var displaced in previous)
                if (!slots.IsEquipped(displaced)) bag.Add(displaced);
        }
        reason = null;
        return true;
    }
}
