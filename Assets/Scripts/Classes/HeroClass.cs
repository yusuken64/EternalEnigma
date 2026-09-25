using EternalEnigma.Core.Classes;

// Rules that combine a hero's primary and optional secondary class.
public static class HeroClass
{
	// Null when the hero has no class yet (unassigned prefab).
	public static ClassKit ToKit(ClassDefinition primary, ClassDefinition secondary)
	{
		if (primary == null) return null;
		bool hasSecondary = secondary != null && secondary.Id != primary.Id;
		return new ClassKit(primary.ToSkillTable(), hasSecondary ? secondary.ToSkillTable() : null);
	}

	// A hero with no class can use anything (keeps unassigned prefabs working).
	public static bool AllowsWeapon(ClassDefinition primary, ClassDefinition secondary, WeaponType type)
	{
		if (primary == null) return true;
		return primary.AllowsWeapon(type) || (secondary != null && secondary.AllowsWeapon(type));
	}

	// Accessories are never class-restricted; weapons and off-hand items are.
	public static bool AllowsItem(ClassDefinition primary, ClassDefinition secondary, EquipableInventoryItem item)
	{
		var definition = item?.EquipmentItemDefinition;
		if (definition == null || definition.EquipmentSlot == EquipmentSlot.Accessory) return true;
		return AllowsWeapon(primary, secondary, definition.WeaponType);
	}

	// "Warrior", "Warrior / Scout", or "" when there is no class.
	public static string Label(ClassDefinition primary, ClassDefinition secondary)
	{
		if (primary == null) return "";
		return secondary != null && secondary.Id != primary.Id
			? $"{primary.DisplayName} / {secondary.DisplayName}" : primary.DisplayName;
	}

	// Per-level growth. No class (or no growth set) keeps today's flat +2 Strength / +5 HPMax.
	public static StatModification Growth(ClassDefinition primary)
	{
		if (primary != null && primary.GrowthPerLevel != null) return primary.GrowthPerLevel;
		return new StatModification { Strength = 2, HPMax = 5 };
	}
}
