using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Equipment : MonoBehaviour
{
	public EquipableInventoryItem EquipedWeapon;
	public EquipableInventoryItem EquipedShield;
	public EquipableInventoryItem EquipedAccessory;

	public delegate void EquipmentChangedEventHandler(EquipChangeType equipChangeType, EquipableInventoryItem item);
	public event EquipmentChangedEventHandler HandleEquipmentChanged;

	internal IEnumerable<EquipableInventoryItem> GetEquippedItems()
	{
		if (EquipedWeapon?.ItemDefinition != null) yield return EquipedWeapon;
		if (EquipedShield?.ItemDefinition != null) yield return EquipedShield;
		if (EquipedAccessory?.ItemDefinition != null) yield return EquipedAccessory;
	}

	public StatModification GetEquipmentStatModification()
	{
		return EquipedWeapon?.GetEquipmentStatModification() +
			EquipedShield?.GetEquipmentStatModification() +
			EquipedAccessory?.GetEquipmentStatModification();
	}

	public void Equip(EquipableInventoryItem equipableInventoryItem)
	{
		UnEquip(equipableInventoryItem.EquipmentSlot);

		switch (equipableInventoryItem.EquipmentSlot)
		{
			case EquipmentSlot.TwoHand:
				EquipedWeapon = equipableInventoryItem;
				EquipedShield = null;
				break;
			case EquipmentSlot.MainHand:
				EquipedWeapon = equipableInventoryItem;
				break;
			case EquipmentSlot.OffHand:
				EquipedShield = equipableInventoryItem;
				if (EquipedWeapon?.ItemDefinition is EquipmentItemDefinition equipment)
				{
					if (equipment.WeaponType == WeaponType.TwoHandSword)
					{
						EquipedWeapon = null;
					}
				}
				break;
			case EquipmentSlot.Accessory:
				EquipedAccessory = equipableInventoryItem;
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
				unequippedItem = EquipedWeapon;
				EquipedWeapon = null;
				break;

			case EquipmentSlot.OffHand:
				unequippedItem = EquipedShield;
				EquipedShield = null;
				break;

			case EquipmentSlot.Accessory:
				unequippedItem = EquipedAccessory;
				EquipedAccessory = null;
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
			EquipedWeapon == x ||
			EquipedShield == x ||
			EquipedAccessory == x;
	}
}

public enum EquipChangeType
{
	Equip,
	UnEquip
}