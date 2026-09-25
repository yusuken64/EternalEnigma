using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RestoreSPAction : GameAction
{
	public int Amount = 2;
	public bool ExcludeCaster;
	[NonSerialized] private Character caster;
	[NonSerialized] private Character target;
	[NonSerialized] private int resolved;

	public static RestoreSPAction Bound(Character caster, Character target, int amount) =>
		new RestoreSPAction { Amount = amount, caster = caster, target = target };

	internal override GameAction AsTargetedSkill(Character caster, Character target)
	{
		return AsTargetedSkill(caster, target, SkillRankContext.Unranked);
	}

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank)
	{
		var a = Bound(caster, target, rank.Scaling != null ? rank.Scaling.ScalePower(Amount, rank.Rank) : Amount);
		a.ExcludeCaster = ExcludeCaster;
		return a;
	}

	internal override bool IsValid(Character character)
	{
		return target != null && target.Vitals.HP > 0 && !(ExcludeCaster && target == caster);
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (!IsValid(character)) return new List<GameAction>();

		int cap = target.FinalStats.SPMax;
		resolved = Math.Max(0, Math.Min(Amount, cap - target.Vitals.SP));
		AddMetricsModification(target, (stats, vitals) => vitals.SP = Math.Min(vitals.SP + resolved, cap));
		return new List<GameAction>();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation || resolved <= 0) yield break;
		Game.Instance.DoFloatingText("+" + resolved + " SP", Color.cyan, target.VisualParent.transform.position);
		yield return null;
	}
}
