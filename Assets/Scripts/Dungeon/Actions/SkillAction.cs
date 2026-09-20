using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class SkillAction : GameAction
{
	private Character caster;
	private Skill skill;
	private Character target;
	private InventoryItem inventoryTarget;
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

	internal static SkillAction ForInventoryItem(Character caster, Skill skill, InventoryItem item) =>
		new SkillAction(caster, skill, null) { inventoryTarget = item };

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (!IsValid(character)) return new();
		bool inventoryTargeting = skill.Targeting == SkillTargeting.InventoryItem;
		affected = inventoryTargeting ? new List<Character> { caster } : skill.GetAffectedCharacters(caster, target);
		AddMetricsModification(
			caster,
			(stats, vitals) =>
			{
				vitals.SP -= skill.SPCost;
			});

		return inventoryTargeting ? skill.GetInventoryEffects(caster, inventoryTarget) :
			affected.SelectMany(recipient => skill.GetEffects(caster, recipient)).ToList();
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
			(skill.Targeting == SkillTargeting.InventoryItem ?
				skill.GetInventoryTargets(caster).Contains(inventoryTarget) :
				inventoryTarget == null && skill.GetAffectedCharacters(caster, target).Any());
	}
}
