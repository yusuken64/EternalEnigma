using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ScaledHealAction : GameAction
{
	public int BaseHeal = 10;
	public float PerLevel = 2f;
	[NonSerialized] private Character caster;
	[NonSerialized] private Character target;
	[NonSerialized] private SkillRankContext rank;

	override internal bool IsValid(Character character)
	{
		return caster != null && target != null && target.Vitals.HP > 0;
	}

	override internal IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		yield break;
	}

	override internal List<GameAction> ExecuteImmediate(Character character)
	{
		if (!IsValid(character))
		{
			return new List<GameAction>();
		}

		int heal = Mathf.FloorToInt(BaseHeal + PerLevel * Math.Max(1, caster.Vitals.Level));

		if (rank.Scaling != null)
		{
			heal = rank.Scaling.ScalePower(heal, rank.Rank);
		}

		heal = Math.Max(1, Mathf.RoundToInt(heal * ClassPassives.HealingMultiplier(caster, target)));

		return new List<GameAction> { new TakeHealAction(caster, target, heal) };
	}

	override internal GameAction AsTargetedSkill(Character caster, Character target)
	{
		return AsTargetedSkill(caster, target, SkillRankContext.Unranked);
	}

	override internal GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank)
	{
		var action = new ScaledHealAction { BaseHeal = BaseHeal, PerLevel = PerLevel };
		action.caster = caster;
		action.target = target;
		action.rank = rank;
		return action;
	}
}
