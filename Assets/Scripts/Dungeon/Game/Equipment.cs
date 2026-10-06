using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Equipment : MonoBehaviour
{
	public EquipableInventoryItem EquippedWeapon;
	public EquipableInventoryItem EquippedShield;
	public EquipableInventoryItem EquippedAccessory;
	public Loadout Current() => new Loadout(
		EquippedWeapon?.EquipmentItemDefinition != null ? EquippedWeapon : null,
		EquippedShield?.EquipmentItemDefinition != null ? EquippedShield : null,
		EquippedAccessory?.EquipmentItemDefinition != null ? EquippedAccessory : null);

	public delegate void EquipmentChangedEventHandler(EquipChangeType equipChangeType, EquipableInventoryItem item);
	public event EquipmentChangedEventHandler HandleEquipmentChanged;

    internal void RestoreSaved(IEnumerable<ItemSaveData> saved, ItemManager manager, Action<InventoryItem> displaced)
    {
        var items = saved.Select(item => item.Restore(manager)).OfType<EquipableInventoryItem>().ToList();
        EquippedWeapon = EquippedShield = EquippedAccessory = null;
        foreach (var item in items)
        {
            var before = GetEquippedItems().ToArray();
            Equip(item);
            foreach (var old in before.Where(old => !IsEquipped(old))) displaced?.Invoke(old);
        }
        // Saved equipment replaces prefab defaults; saved conflicts are all returned above.
    }

	// Optional class restriction used by player-initiated equips (CanEquip). Null allows everything.
	// Equip() itself never checks it, so restoring saved/starting equipment is unaffected.
	[NonSerialized] public Func<EquipableInventoryItem, bool> ClassFilter;

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
		if (newItem == null || IsEquipped(newItem)) return;
		var previousItems = GetEquippedItems().ToArray();
		var slots = new Dictionary<EquipmentSlot, EquipableInventoryItem>();

		if (EquippedWeapon?.EquipmentItemDefinition != null)
			slots[EquipmentSlot.MainHand] = EquippedWeapon;
		if (EquippedShield?.EquipmentItemDefinition != null)
			slots[EquipmentSlot.OffHand] = EquippedShield;
		if (EquippedAccessory?.EquipmentItemDefinition != null)
			slots[EquipmentSlot.Accessory] = EquippedAccessory;

		ApplyEquipChange(slots, newItem);

		// Now commit the changes back
		slots.TryGetValue(EquipmentSlot.MainHand, out EquippedWeapon);
		slots.TryGetValue(EquipmentSlot.OffHand, out EquippedShield);
		slots.TryGetValue(EquipmentSlot.Accessory, out EquippedAccessory);

		HandleEquipmentChanged?.Invoke(EquipChangeType.Equip, newItem);
		foreach (var displaced in previousItems.Where(item => !IsEquipped(item)))
			HandleEquipmentChanged?.Invoke(EquipChangeType.UnEquip, displaced);
	}

	private static void ApplyEquipChange(
	Dictionary<EquipmentSlot, EquipableInventoryItem> slots,
	EquipableInventoryItem newItem)
	{
		bool bow = newItem.EquipmentItemDefinition.WeaponType == WeaponType.BowAndArrow && !newItem.EquipmentItemDefinition.IsAmmunition;
        if (bow)
        {
            slots[EquipmentSlot.MainHand] = newItem;
            if (slots.TryGetValue(EquipmentSlot.OffHand, out var offhand) && !ArrowSupply.IsArrow(offhand)) slots.Remove(EquipmentSlot.OffHand);
            return;
        }
        if (newItem.EquipmentSlot == EquipmentSlot.OffHand && !ArrowSupply.IsArrow(newItem) &&
            slots.TryGetValue(EquipmentSlot.MainHand, out var main) && main.EquipmentItemDefinition.WeaponType == WeaponType.BowAndArrow)
            slots.Remove(EquipmentSlot.MainHand);
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
					currentMain?.EquipmentSlot == EquipmentSlot.TwoHand)
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
		return Current().With(newItem).Modification;
	}

	internal bool CanEquip(EquipableInventoryItem equipableInventoryItem)
	{
		return equipableInventoryItem?.EquipmentItemDefinition != null && !IsEquipped(equipableInventoryItem) &&
			(ClassFilter == null || ClassFilter(equipableInventoryItem));
	}

	internal void UnEquip(EquipableInventoryItem equipableInventoryItem)
	{
		if (!IsEquipped(equipableInventoryItem)) return;
		UnEquip(EquippedWeapon == equipableInventoryItem ? EquipmentSlot.MainHand : EquippedShield == equipableInventoryItem ? EquipmentSlot.OffHand : EquipmentSlot.Accessory);
	}

	private void UnEquip(EquipmentSlot slot)
	{
		EquipableInventoryItem unequippedItem = null;

		switch (slot)
		{
			case EquipmentSlot.TwoHand:
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
