using System;

[Serializable]
public class EquipableInventoryItem : InventoryItem
{
	private readonly EquipmentItemDefinition equipmentItemDefinition;
	public EquipmentSlot EquipmentSlot => equipmentItemDefinition.EquipmentSlot;
	public EquipableInventoryItem(EquipmentItemDefinition equipmentItemDefinition, int? stock = null) : base(equipmentItemDefinition, stock)
	{
		this.equipmentItemDefinition = equipmentItemDefinition;
	}

	public StatModification GetEquipmentStatModification()
	{
		if (equipmentItemDefinition == null) { return new(); }
		return equipmentItemDefinition.GetEquipmentStatModification();
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
