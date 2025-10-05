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

	public void Equip(EquipableInventoryItem newItem)
	{
		var slots = new Dictionary<EquipmentSlot, EquipableInventoryItem>();

		if (EquippedWeapon != null)
			slots[EquipmentSlot.MainHand] = EquippedWeapon;
		if (EquippedShield != null)
			slots[EquipmentSlot.OffHand] = EquippedShield;
		if (EquippedAccessory != null)
			slots[EquipmentSlot.Accessory] = EquippedAccessory;

		ApplyEquipChange(slots, newItem);

		// Now commit the changes back
		slots.TryGetValue(EquipmentSlot.MainHand, out EquippedWeapon);
		slots.TryGetValue(EquipmentSlot.OffHand, out EquippedShield);
		slots.TryGetValue(EquipmentSlot.Accessory, out EquippedAccessory);

		HandleEquipmentChanged?.Invoke(EquipChangeType.Equip, newItem);
	}

	private static void ApplyEquipChange(
	Dictionary<EquipmentSlot, EquipableInventoryItem> slots,
	EquipableInventoryItem newItem)
	{
		switch (newItem.EquipmentSlot)
		{
			case EquipmentSlot.TwoHand:
				slots[EquipmentSlot.MainHand] = newItem;
				slots.Remove(EquipmentSlot.OffHand);
				break;

			case EquipmentSlot.MainHand:
				// If there’s a two-hander equipped, it’s replaced
				slots[EquipmentSlot.MainHand] = newItem;
				break;

			case EquipmentSlot.OffHand:
				// If main hand is a two-hander, remove it
				if (slots.TryGetValue(EquipmentSlot.MainHand, out var currentMain) &&
					currentMain?.EquipmentItemDefinition?.WeaponType == WeaponType.TwoHandSword)
				{
					slots.Remove(EquipmentSlot.MainHand);
				}
				slots[EquipmentSlot.OffHand] = newItem;
				break;

			case EquipmentSlot.Accessory:
				slots[EquipmentSlot.Accessory] = newItem;
				break;
		}
	}

	public StatModification GetStatsIfEquipped(EquipableInventoryItem newItem)
	{
		var slots = new Dictionary<EquipmentSlot, EquipableInventoryItem>();

		if (EquippedWeapon != null)
			slots[EquipmentSlot.MainHand] = EquippedWeapon;
		if (EquippedShield != null)
			slots[EquipmentSlot.OffHand] = EquippedShield;
		if (EquippedAccessory != null)
			slots[EquipmentSlot.Accessory] = EquippedAccessory;

		ApplyEquipChange(slots, newItem);

		// Now sum up stats from simulated slots
		StatModification total = new();
		foreach (var kvp in slots)
		{
			total += kvp.Value.GetEquipmentStatModification();
		}

		return total;
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