using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ItemManager : MonoBehaviour
{
	public List<ItemDefinition> ItemDefinitions;

	public InventoryItem GetAsInventoryItem(ItemDefinition itemDefinition, int? stock)
	{
		return itemDefinition.AsInventoryItem(stock);
	}

	internal InventoryItem GetAsInventoryItemByName(string itemName, int? stock = null)
	{
		var itemDefinition = ItemDefinitions.First(x => x.ItemName == itemName);
		return itemDefinition.AsInventoryItem(stock);
	}

	internal ItemDefinition GetRandomDrop(Enemy enemy)
	{
		return ItemDefinitions[UnityEngine.Random.Range(0, ItemDefinitions.Count())];
	}
}