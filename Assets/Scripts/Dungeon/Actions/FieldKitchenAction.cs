using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Skill effect. Author on a skill with Targeting = Self. Spends one food item to restore hunger to standing party members.
[Serializable]
public class FieldKitchenAction : GameAction, ISkillCastCondition
{
	public static readonly HashSet<string> FoodNames = new() { "Bread" };
	public int HungerRestore = 30;
	[NonSerialized] private Character caster;
	[NonSerialized] private int amount;

	public FieldKitchenAction() { }

	internal override GameAction AsTargetedSkill(Character caster, Character target) =>
		AsTargetedSkill(caster, target, SkillRankContext.Unranked);

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank) =>
		new FieldKitchenAction
		{
			caster = caster,
			amount = rank.Scaling != null ? rank.Scaling.ScalePower(HungerRestore, rank.Rank) : HungerRestore,
		};

	private static InventoryItem FindFood() =>
		Game.Instance?.PlayerController?.Inventory?.InventoryItems.FirstOrDefault(i => i != null && FoodNames.Contains(i.ItemName));

	public bool CanCast(Character caster, out string reason)
	{
		bool hasFood = FindFood() != null;
		reason = hasFood ? "" : "No food in the bag.";
		return hasFood;
	}

	internal override bool IsValid(Character character) =>
		caster != null && caster.Vitals.HP > 0 && FindFood() != null;

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (!IsValid(character)) return new();

		var food = FindFood();
		var inventory = Game.Instance.PlayerController.Inventory;

		if (!AutoplayRunner.InfiniteResourcesFor(caster))
		{
			if (food.HasStacks) food.Decrement();
			if (!food.HasStacks || food.ShouldRemoveAfterUse()) inventory.Remove(food);
		}

		foreach (var a in PartyRules.StandingMembers(Game.Instance))
		{
			int max = a.FinalStats.HungerMax;
			int add = amount;
			AddMetricsModification(a, (stats, vitals) => vitals.Hunger = Math.Min(vitals.Hunger + add, max));
		}

		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		yield break;
	}
}
