using System.Collections;
using System.Collections.Generic;
using System.Linq;

internal class AddInventoryItemAction : GameAction
{
	private readonly Inventory inventory;
	private ItemDefinition itemDefinition;

	public AddInventoryItemAction(Inventory inventory, ItemDefinition itemDefinition)
	{
		this.inventory = inventory;
		this.itemDefinition = itemDefinition;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		inventory.Add(itemDefinition.AsInventoryItem(null));
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
    {
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return inventory.Count() < 4;
	}
}
