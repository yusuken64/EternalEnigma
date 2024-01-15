using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Inventory : MonoBehaviour
{
	public List<InventoryItem> InventoryItems = new();

	public EquipableInventoryItem EquipedWeapon;
	public EquipableInventoryItem EquipedShield;
	public EquipableInventoryItem EquipedAccessory;

	public int MaxItems = 10;

	public delegate void EquipmentChangedEventHandler();
	public event EquipmentChangedEventHandler HandleEquipmentChanged;

	internal void Remove(InventoryItem item)
	{
		if (item == EquipedWeapon) { UnEquip(EquipmentSlot.MainHand); }
		if (item == EquipedShield) { UnEquip(EquipmentSlot.OffHand); }
		if (item == EquipedAccessory) { UnEquip(EquipmentSlot.Accessory); }
		InventoryItems.Remove(item);
	}

	internal void Add(InventoryItem inventoryItem)
	{
		InventoryItems.Add(inventoryItem);
	}

	internal int Count()
	{
		return InventoryItems.Count();
	}

	public void Equip(EquipableInventoryItem equipableInventoryItem)
	{
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

		if (InventoryItems.Contains(equipableInventoryItem))
		{
			InventoryItems.Remove(equipableInventoryItem);
			InventoryItems.Insert(0, equipableInventoryItem);
			HandleEquipmentChanged?.Invoke();
		}
	}

	internal bool CanAdd()
	{
		return InventoryItems.Count() < MaxItems;
	}

	internal bool CanEquip(EquipableInventoryItem equipableInventoryItem)
	{
		return InventoryItems.Contains(equipableInventoryItem) &&
			!IsEquipped(equipableInventoryItem);
	}

	internal void UnEquip(EquipableInventoryItem equipableInventoryItem)
	{
		UnEquip(equipableInventoryItem.EquipmentSlot);
		HandleEquipmentChanged?.Invoke();
	}

	public void UnEquip(EquipmentSlot slot)
	{
		switch (slot)
		{
			case EquipmentSlot.MainHand:
				EquipedWeapon = null;
				break;
			case EquipmentSlot.OffHand:
				EquipedWeapon = null;
				break;
			case EquipmentSlot.Accessory:
				EquipedWeapon = null;
				break;
		}
	}

	public StatModification GetEquipmentStatModification()
	{
		return EquipedWeapon?.GetEquipmentStatModification() +
			EquipedShield?.GetEquipmentStatModification() +
			EquipedAccessory?.GetEquipmentStatModification();
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
