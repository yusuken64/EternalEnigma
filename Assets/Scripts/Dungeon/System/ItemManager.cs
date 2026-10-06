using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ItemManager : MonoBehaviour
{
	public List<ItemDefinition> ItemDefinitions;
	public List<ItemDefinition> StartingItems;
    public List<ItemDefinition> DungeonLoot => ItemDefinitions.Where(i => i != null && !i.ShopOnly && !i.IsProgressionItem).ToList();

	public InventoryItem GetAsInventoryItem(ItemDefinition itemDefinition, int? stock)
	{
		return itemDefinition.AsInventoryItem(stock);
	}

	internal InventoryItem GetAsInventoryItemByName(string itemName, int? stock = null)
	{
		var itemDefinition = ItemDefinitions.FirstOrDefault(x => x.ItemName == itemName) ??
			StartingItems?.FirstOrDefault(x => x.ItemName == itemName) ??
			Resources.LoadAll<TownBuildingDefinition>("Towns/Buildings")
				.SelectMany(building => building.ShopCatalog)
				.Select(offer => offer.Item)
				.FirstOrDefault(item => item != null && item.ItemName == itemName) ??
			DemoDungeonLoadout.Load()?.Items.FirstOrDefault(x => x.ItemName == itemName) ??
			(ItemDefinition)MaterialCatalog.Find(itemName) ??
			Resources.LoadAll<ItemDefinition>("TrapFood").FirstOrDefault(x => x.ItemName == itemName);
		if (itemDefinition == null) throw new InvalidOperationException($"Unknown item '{itemName}'.");
		return itemDefinition.AsInventoryItem(stock);
	}

	internal ItemDefinition GetRandomDrop(Character enemy)
	{
		var loot = DungeonLoot;
        if (loot.Count == 0) throw new InvalidOperationException("Dungeon loot is empty.");
        return loot[UnityEngine.Random.Range(0, loot.Count)];
	}

	/// <summary>Deterministic pick by roll over ItemDefinitions in list order.</summary>
	internal ItemDefinition GetRandomDrop(int roll)
	{
		var loot = DungeonLoot;
        if (loot.Count == 0) throw new InvalidOperationException("Dungeon loot is empty.");
        return loot[(int)((uint)roll % (uint)loot.Count)];
	}
}
