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

	internal ItemDefinition GetRandomDrop(Character enemy) => GetRandomDrop(enemy, 1);

	internal ItemDefinition GetRandomDrop(Character enemy, int floor) =>
		GetRandomDrop(UnityEngine.Random.Range(0, int.MaxValue), floor);

	/// <summary>Deterministic floor-aware pick, retaining list order within each item category.</summary>
	internal ItemDefinition GetRandomDrop(int roll) => GetRandomDrop(roll, 1);

	internal ItemDefinition GetRandomDrop(int roll, int floor)
	{
		var loot = DungeonLoot.Where(i => i.MinFloor <= floor && floor <= i.MaxFloor).ToList();
		if (loot.Count == 0) throw new InvalidOperationException($"Dungeon loot has no eligible item on floor {floor}.");
		var consumables = loot.Where(i => i is not EquipmentItemDefinition).ToList();
		var equipment = loot.Where(i => i is EquipmentItemDefinition).ToList();
		uint value = (uint)roll;
		bool chooseConsumable = consumables.Count > 0 && (equipment.Count == 0 || value % 100 < 28);
		var pool = chooseConsumable ? consumables : equipment;
		return pool[(int)((value / 100) % (uint)pool.Count)];
	}
}
