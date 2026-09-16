using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class SkillAction : GameAction
{
	private Character caster;
	private Skill skill;
	private Character target;

	public SkillAction()
	{

	}
	public SkillAction(Character caster, Skill skill, Character target)
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
		if (skipAnimation) yield break;
		yield return skill.ExecuteRoutine(caster, target);
	}

    internal override IEnumerable<Vector3Int> AnimationCells(Character actor)
    {
        foreach (var cell in base.AnimationCells(actor)) yield return cell;
        foreach (var cell in AnimationPath(actor, target.TilemapPosition)) yield return cell;
    }

	internal override bool IsValid(Character character)
	{
		return skill.IsValid(caster);
	}
}
