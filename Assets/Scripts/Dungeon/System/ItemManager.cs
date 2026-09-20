using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ItemManager : MonoBehaviour
{
	public List<ItemDefinition> ItemDefinitions;
	public List<ItemDefinition> StartingItems;

	public InventoryItem GetAsInventoryItem(ItemDefinition itemDefinition, int? stock)
	{
		return itemDefinition.AsInventoryItem(stock);
	}

	internal InventoryItem GetAsInventoryItemByName(string itemName, int? stock = null)
	{
		var itemDefinition = ItemDefinitions.FirstOrDefault(x => x.ItemName == itemName) ??
			DemoDungeonLoadout.Load()?.Items.FirstOrDefault(x => x.ItemName == itemName);
		if (itemDefinition == null) throw new InvalidOperationException($"Unknown item '{itemName}'.");
		return itemDefinition.AsInventoryItem(stock);
	}

	internal ItemDefinition GetRandomDrop(Character enemy)
	{
		return ItemDefinitions[UnityEngine.Random.Range(0, ItemDefinitions.Count())];
	}
}
