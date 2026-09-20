using System;

[Serializable]
public class EquipableInventoryItem : InventoryItem
{
	// Use the serialized definition; Unity does not restore constructor-only readonly fields.
	public EquipmentItemDefinition EquipmentItemDefinition => ItemDefinition as EquipmentItemDefinition;
	public EquipmentSlot EquipmentSlot => EquipmentItemDefinition.EquipmentSlot;
	public EquipableInventoryItem(EquipmentItemDefinition equipmentItemDefinition, int? stock = null) : base(equipmentItemDefinition, stock)
	{
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
