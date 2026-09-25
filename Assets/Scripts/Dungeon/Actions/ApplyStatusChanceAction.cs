using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class ApplyStatusChanceAction : GameAction
{
	public StatusEffect StatusEffect;   // prefab
	[Range(0f, 1f)] public float Chance = 1f;
	public bool IsAilment;              // Occultist ailment-chance passives apply
	public bool OnCaster;               // apply to the caster instead of the recipient (Parry Stance)
	[NonSerialized] private Character caster;
	[NonSerialized] private Character target;
	[NonSerialized] private SkillRankContext rank;

	public ApplyStatusChanceAction() { }

	internal override GameAction AsTargetedSkill(Character caster, Character target)
	{
		return AsTargetedSkill(caster, target, SkillRankContext.Unranked);
	}

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank)
	{
		var action = new ApplyStatusChanceAction
		{
			StatusEffect = StatusEffect,
			Chance = Chance,
			IsAilment = IsAilment,
			OnCaster = OnCaster,
			caster = caster,
			target = OnCaster ? caster : target,
			rank = rank
		};
		return action;
	}

	internal override bool IsValid(Character character)
	{
		return StatusEffect != null && target != null && target.Vitals.HP > 0;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		// Check if target is immune
		if (ClassPassives.IsImmune(target, StatusEffect))
		{
			return new();
		}

		// Calculate chance with scaling and ailment bonus
		float chance = Chance;
		if (chance < 1f && rank.Scaling != null)
		{
			chance = rank.Scaling.ScaleChance(chance, rank.Rank);
		}
		if (IsAilment)
		{
			chance += ClassPassives.AilmentChanceBonus(caster);
		}

		// Roll for resistance
		if (chance < 1f && UnityEngine.Random.value >= chance)
		{
			return new();
		}

		// Apply the status effect
		var result = new List<GameAction>
		{
			new ApplyStatusEffectAction { StatusEffect = StatusEffect }.AsTargetedSkill(caster, target, rank)
		};

		// Apply duration bonus if any
		int bonus = ClassPassives.StatusDurationBonus(caster, StatusEffect);
		if (bonus > 0)
		{
			result.Add(new ExtendStatusAction(target, StatusEffect.StackKey, bonus));
		}

		return result;
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		yield break;
	}
}

internal class ExtendStatusAction : GameAction
{
	private Character target;
	private string stackKey;
	private int turns;

	public ExtendStatusAction() { }

	public ExtendStatusAction(Character target, string stackKey, int turns)
	{
		this.target = target;
		this.stackKey = stackKey;
		this.turns = turns;
	}

	internal override bool IsValid(Character character)
	{
		return target != null;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		var status = target.StatusEffects.FirstOrDefault(s => s != null && !s.IsExpired() && s.StackKey == stackKey);
		if (status != null)
		{
			status.TurnsLeft += turns;
		}
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		yield break;
	}
}
