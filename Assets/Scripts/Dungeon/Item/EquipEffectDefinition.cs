using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EquipEffectDefinition", menuName = "Game/ItemEffect/EquipEffectDefinition")]
public class EquipEffectDefinition : ItemEffectDefinition
{
	public override List<GameAction> GetGameActions(Character attacker, Character target, InventoryItem item)
	{
		var equipableItem = item as EquipableInventoryItem;
		if (attacker is Player player)
		{
			if (player.Inventory.IsEquipped(equipableItem))
			{
				//unequip
				return new List<GameAction>()
				{
					new UnEquipAction(player, item as EquipableInventoryItem)
				};
			}
			else
			{
				if (player.Inventory.CanEquip(equipableItem))
				{
					return new List<GameAction>()
					{
						new EquipAction(player, item as EquipableInventoryItem)
					};
				}
			}
		}

		return new();
	}
}
