using System;
using System.Collections.Generic;
using UnityEngine;

// A non-destructive inventory-targeted effect: inspect the selected copy.
[Serializable]
public class InspectInventoryItemEffect : InventorySkillEffect
{
    public static string Describe(InventoryItem item) => item == null ? "No item selected" :
        item.ItemName + ": " + (item is EquipableInventoryItem equipment ?
        $"{equipment.EquipmentSlot}: Strength +{equipment.GetEquipmentStatModification().Strength}" :
        item.HasStacks ? $"Stock: {item.StackStock}" : "Single-use item");
    internal override GameAction Bind(Character caster, InventoryItem item) => new DynamicGameAction(
        _ =>
        {
            Game.Instance.DoFloatingText(Describe(item), Color.cyan, caster);
            return new List<GameAction>();
        }, null, () => item?.ItemDefinition != null);
}
