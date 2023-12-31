using System;
using System.Collections.Generic;

[Serializable]
public class InventoryItem
{
	public string ItemName => ItemDefinition.ItemName;

	public bool HasStacks => ItemDefinition.HasStacks;

	public ItemDefinition ItemDefinition;

	public int? StackStock;

	public InventoryItem(ItemDefinition itemDefinition, int? stock = null)
	{
		ItemDefinition = itemDefinition;
		StackStock = stock;
	}

	internal List<GameAction> GetGameActions(Character attacker, Character target, InventoryItem item)
	{
		return this.ItemDefinition.ItemEffectDefinition.GetGameActions(attacker, target, item);
	}

	internal void Decrement()
	{
		StackStock--;
	}

	internal bool StackIsEmpty()
	{
		return StackStock.Value <= 0;
	}
}
