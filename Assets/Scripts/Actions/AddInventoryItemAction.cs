using System.Collections;
using System.Collections.Generic;
using System.Linq;

internal class AddInventoryItemAction : GameAction
{
	private readonly Character attacker;
	private ItemDefinition itemDefinition;

	public AddInventoryItemAction(Character attacker, ItemDefinition itemDefinition)
	{
		this.attacker = attacker;
		this.itemDefinition = itemDefinition;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (attacker is Player player)
		{
			player.Inventory.Add(itemDefinition.AsInventoryItem(null));
		}

		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		if (attacker is Player player)
		{
			return player.Inventory.Count() < 4;
		}

		return false;
	}
}