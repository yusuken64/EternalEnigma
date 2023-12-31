using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ItemManager : MonoBehaviour
{
	public List<ItemDefinition> ItemDefinitions;

	public InventoryItem GetAsInventoryItem(ItemDefinition itemDefinition, int? stock)
	{
		stock = InitializeStack(itemDefinition, stock);

		if (itemDefinition is EquipmentItemDefinition equipmentItemDefinition)
		{
			return new EquipableInventoryItem(equipmentItemDefinition, stock);
		}
		return new InventoryItem(itemDefinition, stock);
	}

	private static int? InitializeStack(ItemDefinition itemDefinition, int? stock)
	{
		if (stock == null &&
			itemDefinition.HasStacks)
		{
			stock = UnityEngine.Random.Range(itemDefinition.StackStartMin, itemDefinition.StackMax);
		}

		return stock;
	}

	internal InventoryItem GetAsInventoryItemByName(string itemName, int? stock = null)
	{
		var first = ItemDefinitions.First(x => x.ItemName == itemName);
		if (first is EquipmentItemDefinition equipEffectDefinition)
		{
			return new EquipableInventoryItem(equipEffectDefinition);
		}

		stock = InitializeStack(first, stock);
		return new InventoryItem(ItemDefinitions.First(x => x.ItemName == itemName), stock);
	}

	internal ItemDefinition GetRandomDrop(Enemy enemy)
	{
		return ItemDefinitions[UnityEngine.Random.Range(0, ItemDefinitions.Count())];
	}
}