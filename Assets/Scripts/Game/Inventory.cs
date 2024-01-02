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
		if (item == EquipedWeapon) { UnEquip(EquipmentSlot.Weapon); }
		if (item == EquipedShield) { UnEquip(EquipmentSlot.Shield); }
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
			case EquipmentSlot.Weapon:
				EquipedWeapon = equipableInventoryItem;
				break;
			case EquipmentSlot.Shield:
				EquipedShield = equipableInventoryItem;
				break;
			case EquipmentSlot.Accessory:
				EquipedAccessory = equipableInventoryItem;
				break;
		}

		if (InventoryItems.Contains(equipableInventoryItem))
		{
			InventoryItems.Remove(equipableInventoryItem);
			InventoryItems.Insert(0, equipableInventoryItem);
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
	}

	public void UnEquip(EquipmentSlot slot)
	{
		switch (slot)
		{
			case EquipmentSlot.Weapon:
				EquipedWeapon = null;
				break;
			case EquipmentSlot.Shield:
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
