using UnityEngine;

[CreateAssetMenu(fileName = "EquipmentItemDefinition", menuName = "Game/Item/EquipmentItemDefinition")]
public class EquipmentItemDefinition : ItemDefinition
{
	public EquipmentSlot EquipmentSlot;
	public StatModification StatModification;

	internal StatModification GetEquipmentStatModification()
	{
		return StatModification;
	}
}
