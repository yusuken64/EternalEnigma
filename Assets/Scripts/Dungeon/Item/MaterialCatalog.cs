using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MaterialCatalog
{
	public const string IronOre = "Iron Ore";
	public const string HealingHerb = "Healing Herb";
	public const string Timber = "Timber";
	public const string TrapParts = "Trap Parts";
	public const int StackMax = 99;

	private static List<MaterialItemDefinition> all;
	public static IReadOnlyList<MaterialItemDefinition> All => all ??= new List<MaterialItemDefinition>
	{
		Create(IronOre, "Ore gathered with Mining. Sells for 30 gold.", 30, true, EternalEnigma.Core.World.GatheringKind.Ore, DroppedItemVisual.TreasureChest),
		Create(HealingHerb, "A herb gathered with Harvesting. Sells for 20 gold.", 20, true, EternalEnigma.Core.World.GatheringKind.Plant, DroppedItemVisual.Potion),
		Create(Timber, "Wood gathered with Foraging. Sells for 15 gold.", 15, true, EternalEnigma.Core.World.GatheringKind.Forage, DroppedItemVisual.Bread),
		Create(TrapParts, "Salvaged from a disarmed trap. Sells for 25 gold.", 25, false, default, DroppedItemVisual.Key),
	};

	public static MaterialItemDefinition Find(string itemName) => All.FirstOrDefault(m => m.ItemName == itemName);

	public static MaterialItemDefinition ForKind(EternalEnigma.Core.World.GatheringKind kind) => All.First(m => m.HasKind && m.Kind == kind);

	// Adds `amount` units, topping up existing stacks first, then new stacks while the bag has room.
	// Returns the number of units actually added.
	public static int AddToInventory(Inventory inventory, MaterialItemDefinition definition, int amount)
	{
		int added = 0;
		foreach (var stack in inventory.InventoryItems.OfType<MaterialInventoryItem>().Where(i => i.ItemDefinition == definition))
		{
			int room = StackMax - (stack.StackStock ?? 0);
			int take = Mathf.Min(room, amount - added);
			if (take <= 0) continue;
			stack.StackStock = (stack.StackStock ?? 0) + take;
			added += take;
		}
		while (added < amount && inventory.CanAdd())
		{
			int take = Mathf.Min(StackMax, amount - added);
			inventory.Add(definition.AsInventoryItem(take));
			added += take;
		}
		return added;
	}

	private static MaterialItemDefinition Create(string name, string description, int sellValue, bool hasKind,
		EternalEnigma.Core.World.GatheringKind kind, DroppedItemVisual visual)
	{
		var definition = ScriptableObject.CreateInstance<MaterialItemDefinition>();
		definition.name = name;
		definition.hideFlags = HideFlags.DontUnloadUnusedAsset;
		definition.ItemName = name;
		definition.Description = description;
		definition.SellValue = sellValue;
		definition.HasKind = hasKind;
		definition.Kind = kind;
		definition.StackStartMin = 1;
		definition.StackStartMax = 1;
		definition.StackMax = StackMax;
		definition.DroppedItemVisual = visual;
		return definition;
	}
}
