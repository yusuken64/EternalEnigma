using System;

[Serializable]
public class MaterialInventoryItem : InventoryItem
{
	public MaterialInventoryItem(ItemDefinition itemDefinition, int? stock = null) : base(itemDefinition, stock) { }
	internal override bool ShouldRemoveAfterUse() => false;
}
