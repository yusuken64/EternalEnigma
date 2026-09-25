using System;
using System.Collections.Generic;
using System.Linq;

public static class ArrowSupply
{
	// Arrow items are identified by name because the Bolt spell shares RangedAttackItemEffectDefinition.
	public static readonly HashSet<string> ArrowItemNames = new(StringComparer.Ordinal) { "Wooden Arrows" };

	public static bool IsArrow(InventoryItem item) =>
		item?.ItemDefinition != null && item.HasStacks && ArrowItemNames.Contains(item.ItemName);

	public static bool HasBow(Character character) =>
		character?.Equipment != null &&
		character.Equipment.GetEquippedItems().Any(i => i.EquipmentItemDefinition != null &&
			i.EquipmentItemDefinition.WeaponType == WeaponType.BowAndArrow);

	public static int Count(Inventory inventory) =>
		inventory == null ? 0 : inventory.InventoryItems.Where(IsArrow).Sum(i => Math.Max(0, i.StackStock ?? 0));

	public static int Count(Character character) => Count(Game.Instance?.PlayerController?.Inventory);

	// Arrows needed before the skill can be cast: Fixed => ArrowCost, PerTarget => 1, no arrow cost => 0.
	public static int RequiredToCast(Skill skill)
	{
		if (skill == null || skill.ArrowCost <= 0) return 0;
		return skill.ArrowCostMode == ArrowCostMode.PerTarget ? 1 : skill.ArrowCost;
	}

	// Fires up to `count` arrows. Each fired arrow is removed from its stack unless it is recovered
	// (UnityEngine.Random.value < recoveryChance). Empty stacks are removed from the bag. Returns arrows fired.
	public static int Consume(Inventory inventory, int count, float recoveryChance = 0f)
	{
		if (inventory == null || count <= 0) return 0;

		int fired = 0;
		var arrowStacks = inventory.InventoryItems.Where(IsArrow).ToList();

		foreach (var stack in arrowStacks)
		{
			while (fired < count && (stack.StackStock ?? 0) > 0)
			{
				fired++;
				if (UnityEngine.Random.value >= recoveryChance)
				{
					stack.Decrement();
				}
			}

			if (stack.StackIsEmpty())
			{
				inventory.Remove(stack);
			}
		}

		return fired;
	}

	// Party-bag overload. During autoplay with infinite resources, returns min(count, Count(character)) without consuming.
	public static int Consume(Character character, int count, float recoveryChance = 0f)
	{
		if (AutoplayRunner.InfiniteResourcesFor(character))
		{
			return Math.Min(count, Count(character));
		}

		return Consume(Game.Instance?.PlayerController?.Inventory, count, recoveryChance);
	}
}
