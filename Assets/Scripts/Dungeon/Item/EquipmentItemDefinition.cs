using UnityEngine;

[CreateAssetMenu(fileName = "EquipmentItemDefinition", menuName = "Game/Item/EquipmentItemDefinition")]
public class EquipmentItemDefinition : ItemDefinition
{
	public EquipmentSlot EquipmentSlot;
	public StatModification StatModification;
	public string WeaponModelName;
	public string WeaponModelVariant;
	public WeaponType WeaponType;
	public bool IsAmmunition;
	public float ArrowDamageMultiplier = 1f;
	public int ArrowTargets = 1;

	// Shared by offhand animation and weapon-targeting effects.
	public bool IsOffhandMeleeWeapon => EquipmentSlot == EquipmentSlot.OffHand &&
		(WeaponType == WeaponType.OffhandSword || WeaponType == WeaponType.SimpleWeapon ||
		 WeaponType == WeaponType.Axe || WeaponType == WeaponType.Hammer);

	public bool IsRangedAttack;
	public GameObject ProjectilePrefab;

	internal override InventoryItem AsInventoryItem(int? stock)
	{
		return new EquipableInventoryItem(this, stock);
	}

	internal StatModification GetEquipmentStatModification()
	{
		return StatModification;
	}

	public string GetEquipmentDescription()
	{
		switch (WeaponType)
		{
			case WeaponType.SingleSword:
				break;
			case WeaponType.Spear:
				break;
			case WeaponType.BowAndArrow:
				break;
			case WeaponType.TwoHandSword:
				break;
			case WeaponType.MagicWand:
				break;
			case WeaponType.OffhandSword:
				break;
			case WeaponType.OffhandShield:
				break;
		}
		return "";
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
	SimpleWeapon,
	Axe,
	Hammer,
}
