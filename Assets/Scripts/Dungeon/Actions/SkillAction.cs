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
	private Vector3Int direction;
	private MissileTargeting.Hit missileHit;
	private List<Character> affected = new();

	internal Skill Skill => skill;

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
	internal static SkillAction ForMissile(Character caster, Skill skill, Vector3Int direction) =>
		new SkillAction(caster, skill, null) { direction = direction };

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (!IsValid(character)) return new();
		bool inventoryTargeting = skill.Targeting == SkillTargeting.InventoryItem;
		affected = inventoryTargeting ? new List<Character> { caster } : skill.GetAffectedCharacters(caster, target);
		if (skill.Targeting == SkillTargeting.Missile)
		{
			missileHit = MissileTargeting.Trace(caster, direction, skill.MissileRange + (skill.UsesArrows ? ClassPassives.MissileRangeBonus(caster) : 0));
			affected = skill.TargetingRules.GetMissileAffected(caster, missileHit);
		}
		if (skill.UsesArrows && skill.Targeting != SkillTargeting.InventoryItem)
		{
			if (skill.ArrowCostMode == ArrowCostMode.PerTarget)
			{
				int fired = ArrowSupply.Consume(caster, affected.Count, ClassPassives.ArrowRecoveryChance(caster));
				if (fired < affected.Count) affected = affected.Take(fired).ToList();
			}
			else
			{
				ArrowSupply.Consume(caster, skill.ArrowCost, ClassPassives.ArrowRecoveryChance(caster));
			}
		}
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
		if (skill.Targeting == SkillTargeting.Missile)
			yield return MissileTargeting.Animate(caster, missileHit.Cell, skill.MissileProjectilePrefab);
		foreach (var recipient in affected)
			if (recipient != null) yield return skill.ExecuteRoutine(caster, recipient);
	}

    internal override IEnumerable<Vector3Int> AnimationCells(Character actor)
    {
        foreach (var cell in base.AnimationCells(actor)) yield return cell;
        if (skill?.Targeting == SkillTargeting.Missile)
            foreach (var cell in AnimationPath(actor, missileHit.Cell)) yield return cell;
        foreach (var recipient in affected)
            if (recipient != null)
                foreach (var cell in AnimationPath(actor, recipient.TilemapPosition)) yield return cell;
    }

	internal override bool IsValid(Character character)
	{
		return character == caster && skill != null && skill.IsValid(caster) &&
			(skill.Targeting == SkillTargeting.Missile ? MissileTargeting.IsDirection(direction) : skill.Targeting == SkillTargeting.InventoryItem ?
				skill.GetInventoryTargets(caster).Contains(inventoryTarget) :
				inventoryTarget == null && skill.GetAffectedCharacters(caster, target).Any());
	}
}
