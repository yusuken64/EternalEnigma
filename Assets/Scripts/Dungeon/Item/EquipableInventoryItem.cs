using System;

[Serializable]
public class EquipableInventoryItem : InventoryItem
{
	public readonly EquipmentItemDefinition EquipmentItemDefinition;
	public EquipmentSlot EquipmentSlot => EquipmentItemDefinition.EquipmentSlot;
	public EquipableInventoryItem(EquipmentItemDefinition equipmentItemDefinition, int? stock = null) : base(equipmentItemDefinition, stock)
	{
		this.EquipmentItemDefinition = equipmentItemDefinition;
	}

	public StatModification GetEquipmentStatModification()
	{
		if (EquipmentItemDefinition == null) { return new(); }
		return EquipmentItemDefinition.GetEquipmentStatModification();
	}

	internal override bool ShouldRemoveAfterUse()
	{
		return false;
	}
}

public enum EquipmentSlot
{
	MainHand,
	TwoHand,
	OffHand,
	Accessory
}
