using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class SkillAction : GameAction
{
	private Character caster;
	private Skill skill;
	private Vector3Int target;

	public SkillAction(Character caster, Skill skill, Vector3Int target)
	{
		this.caster = caster;
		this.skill = skill;
		this.target = target;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		AddMetricsModification(
			caster,
			(stats, vitals) =>
			{
				vitals.SP -= skill.SPCost;
			});

		return skill.GetEffects(caster, target);
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
    {
		return skill.ExecuteRoutine(caster);
	}

	internal override bool IsValid(Character character)
	{
		return skill.IsValid(caster);
	}
}
