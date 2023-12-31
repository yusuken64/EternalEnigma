using UnityEngine;

[CreateAssetMenu(fileName = "EquipmentItemDefinition", menuName = "Game/Item/EquipmentItemDefinition")]
public class EquipmentItemDefinition : ItemDefinition
{
	public EquipmentSlot EquipmentSlot;
	public StatModification StatModification;

	internal override InventoryItem AsInventoryItem(int? stock)
	{
		return new EquipableInventoryItem(this, stock);
	}

	internal StatModification GetEquipmentStatModification()
	{
		return StatModification;
	}
}
