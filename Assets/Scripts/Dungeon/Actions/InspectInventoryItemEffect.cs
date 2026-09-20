using System;
using System.Collections.Generic;
using UnityEngine;

// A non-destructive inventory-targeted effect: inspect the selected copy.
[Serializable]
public class InspectInventoryItemEffect : InventorySkillEffect
{
    internal override GameAction Bind(Character caster, InventoryItem item) => new DynamicGameAction(
        _ =>
        {
            string detail = item is EquipableInventoryItem equipment ?
                $"{equipment.EquipmentSlot}: Strength +{equipment.GetEquipmentStatModification().Strength}" :
                item.HasStacks ? $"Stock: {item.StackStock}" : "Single-use item";
            Game.Instance.DoFloatingText($"{item.ItemName}: {detail}", Color.cyan, caster.transform.position);
            return new List<GameAction>();
        }, null, () => item?.ItemDefinition != null);
}
