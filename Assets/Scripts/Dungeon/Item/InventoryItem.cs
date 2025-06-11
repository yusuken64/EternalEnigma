using System;
using System.Collections.Generic;

[Serializable]
public abstract class InventoryItem
{
	public string ItemName => ItemDefinition.ItemName;
	public bool IsConsumable => ItemDefinition.StackMax == 0;
	public bool HasStacks => ItemDefinition.StackMax > 0;

	public ItemDefinition ItemDefinition;
	public int? StackStock;

	public InventoryItem(ItemDefinition itemDefinition, int? stock = null)
	{
		ItemDefinition = itemDefinition;
		stock = itemDefinition.InitializeStack(stock);
		StackStock = stock;
	}

	internal abstract bool ShouldRemoveAfterUse();

	internal List<GameAction> GetGameActions(Character attacker, Character target, Inventory inventory, InventoryItem item)
	{
		return this.ItemDefinition.ItemEffectDefinition.GetGameActions(attacker, target, inventory, item);
	}

	internal void Decrement()
	{
		StackStock--;
	}

	internal bool StackIsEmpty()
	{
		return !StackStock.HasValue || StackStock.Value <= 0;
	}
}
