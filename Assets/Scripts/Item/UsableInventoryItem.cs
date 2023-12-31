using System;

[Serializable]
public class UsableInventoryItem : InventoryItem
{
	public UsableInventoryItem(ItemDefinition itemDefinition, int? stock = null) : base(itemDefinition, stock) { }

	internal override bool ShouldRemoveAfterUse()
	{
		return this.StackIsEmpty();
	}
}
