using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AddInventoryItemEffectDefinition", menuName = "Game/ItemEffect/AddInventoryItemEffectDefinition")]
public class AddInventoryItemEffectDefinition : ItemEffectDefinition
{
	public ItemDefinition ItemDefinition;

	public override List<GameAction> GetGameActions(Character attacker, Character target, Inventory inventory, InventoryItem item)
	{
		return new List<GameAction>()
		{
			new AddInventoryItemAction(inventory, ItemDefinition)
		};
	}
}
