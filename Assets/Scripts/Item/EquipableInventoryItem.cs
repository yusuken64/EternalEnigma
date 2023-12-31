using System;

[Serializable]
public class EquipableInventoryItem : InventoryItem
{
	private readonly EquipmentItemDefinition equipmentItemDefinition;
	public EquipmentSlot EquipmentSlot => equipmentItemDefinition.EquipmentSlot;
	public EquipableInventoryItem(EquipmentItemDefinition equipmentItemDefinition, int? stock = null) : base(equipmentItemDefinition)
	{
		this.equipmentItemDefinition = equipmentItemDefinition;
	}

	public StatModification GetEquipmentStatModification()
	{
		if (equipmentItemDefinition == null) { return new(); }
		return equipmentItemDefinition.GetEquipmentStatModification();
	}
}

public enum EquipmentSlot
{
	Weapon,
	Shield,
	Accessory
}
