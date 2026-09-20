using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class SkillAction : GameAction
{
	private Character caster;
	private Skill skill;
	private Character target;
	private List<Character> affected = new();

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
		if (!IsValid(character)) return new();
		affected = skill.GetAffectedCharacters(caster, target);
		AddMetricsModification(
			caster,
			(stats, vitals) =>
			{
				vitals.SP -= skill.SPCost;
			});

		return affected.SelectMany(recipient => skill.GetEffects(caster, recipient)).ToList();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation) yield break;
		foreach (var recipient in affected)
			if (recipient != null) yield return skill.ExecuteRoutine(caster, recipient);
	}

    internal override IEnumerable<Vector3Int> AnimationCells(Character actor)
    {
        foreach (var cell in base.AnimationCells(actor)) yield return cell;
        foreach (var recipient in affected)
            if (recipient != null)
                foreach (var cell in AnimationPath(actor, recipient.TilemapPosition)) yield return cell;
    }

	internal override bool IsValid(Character character)
	{
		return character == caster && skill != null && skill.IsValid(caster) &&
			skill.GetAffectedCharacters(caster, target).Any();
	}
}
