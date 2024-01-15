using UnityEngine;

[CreateAssetMenu(fileName = "EquipmentItemDefinition", menuName = "Game/Item/EquipmentItemDefinition")]
public class EquipmentItemDefinition : ItemDefinition
{
	public EquipmentSlot EquipmentSlot;
	public StatModification StatModification;
	public string WeaponModelName;
	public WeaponType WeaponType;

	internal override InventoryItem AsInventoryItem(int? stock)
	{
		return new EquipableInventoryItem(this, stock);
	}

	internal StatModification GetEquipmentStatModification()
	{
		return StatModification;
	}
}

public enum WeaponType
{
	SingleSword,
	Spear,
	BowAndArrow,
	TwoHandSword,
	MagicWand,
	OffhandSword,
	OffhandShield,
}