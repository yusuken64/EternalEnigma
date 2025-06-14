using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Equipment : MonoBehaviour
{
	public EquipableInventoryItem EquippedWeapon;
	public EquipableInventoryItem EquippedShield;
	public EquipableInventoryItem EquippedAccessory;

	public delegate void EquipmentChangedEventHandler(EquipChangeType equipChangeType, EquipableInventoryItem item);
	public event EquipmentChangedEventHandler HandleEquipmentChanged;

	internal IEnumerable<EquipableInventoryItem> GetEquippedItems()
	{
		if (EquippedWeapon?.ItemDefinition != null) yield return EquippedWeapon;
		if (EquippedShield?.ItemDefinition != null) yield return EquippedShield;
		if (EquippedAccessory?.ItemDefinition != null) yield return EquippedAccessory;
	}

	public StatModification GetEquipmentStatModification()
	{
		return EquippedWeapon?.GetEquipmentStatModification() +
			EquippedShield?.GetEquipmentStatModification() +
			EquippedAccessory?.GetEquipmentStatModification();
	}

	public void Equip(EquipableInventoryItem equipableInventoryItem)
	{
		UnEquip(equipableInventoryItem.EquipmentSlot);

		switch (equipableInventoryItem.EquipmentSlot)
		{
			case EquipmentSlot.TwoHand:
				EquippedWeapon = equipableInventoryItem;
				EquippedShield = null;
				break;
			case EquipmentSlot.MainHand:
				EquippedWeapon = equipableInventoryItem;
				break;
			case EquipmentSlot.OffHand:
				EquippedShield = equipableInventoryItem;
				if (EquippedWeapon?.ItemDefinition is EquipmentItemDefinition equipment)
				{
					if (equipment.WeaponType == WeaponType.TwoHandSword)
					{
						EquippedWeapon = null;
					}
				}
				break;
			case EquipmentSlot.Accessory:
				EquippedAccessory = equipableInventoryItem;
				break;
		}

		HandleEquipmentChanged?.Invoke(EquipChangeType.Equip, equipableInventoryItem);
	}

	internal bool CanEquip(EquipableInventoryItem equipableInventoryItem)
	{
		return !IsEquipped(equipableInventoryItem);
	}

	internal void UnEquip(EquipableInventoryItem equipableInventoryItem)
	{
		UnEquip(equipableInventoryItem.EquipmentSlot);
	}

	private void UnEquip(EquipmentSlot slot)
	{
		EquipableInventoryItem unequippedItem = null;

		switch (slot)
		{
			case EquipmentSlot.MainHand:
				unequippedItem = EquippedWeapon;
				EquippedWeapon = null;
				break;

			case EquipmentSlot.OffHand:
				unequippedItem = EquippedShield;
				EquippedShield = null;
				break;

			case EquipmentSlot.Accessory:
				unequippedItem = EquippedAccessory;
				EquippedAccessory = null;
				break;
		}

		if (unequippedItem?.ItemDefinition != null)
		{
			HandleEquipmentChanged?.Invoke(EquipChangeType.UnEquip, unequippedItem);
		}
	}

	internal bool IsEquipped(InventoryItem x)
	{
		if (x == null) { return false; }

		return
			EquippedWeapon == x ||
			EquippedShield == x ||
			EquippedAccessory == x;
	}

	internal bool IsRangedAttack(out GameObject projectilePrefab)
	{
		if (EquippedWeapon?.EquipmentItemDefinition?.IsRangedAttack == true &&
			EquippedWeapon.EquipmentItemDefinition.ProjectilePrefab != null)
		{
			projectilePrefab = EquippedWeapon.EquipmentItemDefinition.ProjectilePrefab;
			return true;
		}

		if (EquippedShield?.EquipmentItemDefinition?.IsRangedAttack == true &&
			EquippedShield.EquipmentItemDefinition.ProjectilePrefab != null)
		{
			projectilePrefab = EquippedShield.EquipmentItemDefinition.ProjectilePrefab;
			return true;
		}

		if (EquippedAccessory?.EquipmentItemDefinition?.IsRangedAttack == true &&
			EquippedAccessory.EquipmentItemDefinition.ProjectilePrefab != null)
		{
			projectilePrefab = EquippedAccessory.EquipmentItemDefinition.ProjectilePrefab;
			return true;
		}

		projectilePrefab = null;
		return false;
	}
}

public enum EquipChangeType
{
	Equip,
	UnEquip
}