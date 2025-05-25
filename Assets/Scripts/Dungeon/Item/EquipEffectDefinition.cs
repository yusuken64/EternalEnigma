using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EquipEffectDefinition", menuName = "Game/ItemEffect/EquipEffectDefinition")]
public class EquipEffectDefinition : ItemEffectDefinition
{
	public override List<GameAction> GetGameActions(Character character, Character target, InventoryItem item)
	{
		var equipableItem = item as EquipableInventoryItem;
		if (character.Equipment.IsEquipped(equipableItem))
		{
			//unequip
			return new List<GameAction>()
				{
					new UnEquipAction(character, item as EquipableInventoryItem)
				};
		}
		else
		{
			if (character.Equipment.CanEquip(equipableItem))
			{
				return new List<GameAction>()
					{
						new EquipAction(character, item as EquipableInventoryItem)
					};
			}
		}

		return new();
	}
}
