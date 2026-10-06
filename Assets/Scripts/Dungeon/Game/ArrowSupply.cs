using System;
using System.Linq;

public static class ArrowSupply
{

	public static bool IsArrow(InventoryItem item) =>
		item?.ItemDefinition is EquipmentItemDefinition { IsAmmunition: true } && item.HasStacks;

	public static bool HasBow(Character character) =>
		character?.Equipment != null &&
		character.Equipment.GetEquippedItems().Any(i => i.EquipmentItemDefinition != null &&
			!i.EquipmentItemDefinition.IsAmmunition && i.EquipmentItemDefinition.WeaponType == WeaponType.BowAndArrow);

	public static int Count(Character character) => IsArrow(character?.Equipment?.EquippedShield) ? Math.Max(0, character.Equipment.EquippedShield.StackStock ?? 0) : 0;
    public static float DamageMultiplier(Character character) => IsArrow(character?.Equipment?.EquippedShield) ? character.Equipment.EquippedShield.EquipmentItemDefinition.ArrowDamageMultiplier : 1f;
    public static int Penetration(Character character) => IsArrow(character?.Equipment?.EquippedShield) ? character.Equipment.EquippedShield.EquipmentItemDefinition.ArrowTargets : 1;
    public static int Penetration(Character character, Skill skill) => skill.MissileTargets == 0 ? 0 : skill.UsesArrows ? Math.Max(skill.MissileTargets, Penetration(character)) : skill.MissileTargets;

	// Arrows needed before the skill can be cast: Fixed => ArrowCost, PerTarget => 1, no arrow cost => 0.
	public static int RequiredToCast(Skill skill)
	{
		if (skill == null || !skill.UsesArrows) return 0;
        if (skill.ArrowCost <= 0) return 1;
		return skill.ArrowCostMode == ArrowCostMode.PerTarget ? 1 : skill.ArrowCost;
	}

    // Fires from this character's equipped stack only. Recovery preserves a shot's arrow.
    // Autoplay's infinite-resources mode still requires equipped ammunition.
	public static int Consume(Character character, int count, float recoveryChance = 0f)
	{
		if (AutoplayRunner.InfiniteResourcesFor(character))
		{
			return Math.Min(count, Count(character));
		}

		var stack = character?.Equipment?.EquippedShield;
        if (!IsArrow(stack) || count <= 0) return 0;
        int fired = 0;
        while (fired < count && stack.StackStock > 0)
        { fired++; if (UnityEngine.Random.value >= recoveryChance) stack.Decrement(); }
        if (stack.StackIsEmpty()) character.Equipment.UnEquip(stack);
        return fired;
	}
}
