using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class InventoryTargetSelector
{
    public InventoryTargetType ItemType;
    [Tooltip("Also offer the caster's equipped items, in addition to the shared party bag.")]
    public bool IncludeEquipped;

    internal List<InventoryItem> GetTargets(Character caster)
    {
        var game = Game.Instance;
        // The dungeon has one shared party bag; enemies do not own it.
        if (caster is not Ally ally || game == null || !game.Allies.Contains(ally)) return new();
        IEnumerable<InventoryItem> items = game.PlayerController.Inventory.InventoryItems;
        if (IncludeEquipped && caster.Equipment != null)
            items = items.Concat(caster.Equipment.GetEquippedItems());
        return items.Where(Matches).Distinct().ToList();
    }

    internal bool Matches(InventoryItem item)
    {
        if (item?.ItemDefinition == null) return false;
        if (ItemType == InventoryTargetType.AnyItem) return true;
        if (item is not EquipableInventoryItem equipment || equipment.EquipmentItemDefinition == null) return false;
        if (ItemType == InventoryTargetType.Equipment) return true;
        return ItemType == InventoryTargetType.Weapon &&
            (equipment.EquipmentSlot == EquipmentSlot.MainHand || equipment.EquipmentSlot == EquipmentSlot.TwoHand ||
             (equipment.EquipmentSlot == EquipmentSlot.OffHand && equipment.EquipmentItemDefinition.WeaponType == WeaponType.OffhandSword));
    }
}

public enum InventoryTargetType
{
    AnyItem,
    Equipment,
    Weapon
}
