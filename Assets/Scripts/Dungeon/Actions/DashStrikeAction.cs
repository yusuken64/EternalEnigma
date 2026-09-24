using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class DashStrikeAction : GameAction
{
	public float DamagePercent = 1.5f;

	private Character caster;
	private Character target;
	private SkillRankContext rank;
	private Vector3Int? destination;

	public DashStrikeAction() { }

	internal override GameAction AsTargetedSkill(Character caster, Character target)
	{
		return AsTargetedSkill(caster, target, SkillRankContext.Unranked);
	}

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank)
	{
		var bound = new DashStrikeAction
		{
			DamagePercent = this.DamagePercent,
			caster = caster,
			target = target,
			rank = rank
		};
		return bound;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (caster == null || target == null || target.Vitals.HP <= 0)
			return new();

		var dir = SkillMovement.Step(caster.TilemapPosition, target.TilemapPosition);
		int distance = TileWorldDungeon.ChevDistance(caster.TilemapPosition, target.TilemapPosition);
		var cell = SkillMovement.LastFreeAlong(caster, dir, distance);
		if (cell != caster.TilemapPosition)
		{
			SkillMovement.Place(caster, cell);
			destination = cell;
		}

		if (!caster.GetAttackBounds().Overlaps2D(target.ToBounds()))
			return new();

		TrackAnimationTarget(target);
		AttackAction.GetAttackDamage(caster, target, out bool hit, out int damage);
		int scaled = rank.Scaling.ScalePower(Mathf.RoundToInt(damage * DamagePercent), rank.Rank);
		return new() { new TakeDamageAction(caster, target, Math.Max(0, scaled), true, !hit) };
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (destination == null)
			yield break;

		if (skipAnimation)
		{
			SkillMovement.SnapTo(caster, destination.Value);
		}
		else
		{
			yield return SkillMovement.AnimateMove(caster, destination.Value);
		}

		if (target != null)
			caster.SetFacingByTargetPosition(target.TilemapPosition);
	}

	internal override void AddDestinationSight(HashSet<Vector3Int> tiles)
	{
		if (destination.HasValue)
			AddAllySight(tiles, caster, destination.Value);
	}

	internal override IEnumerable<Vector3Int> AnimationCells(Character actor)
	{
		foreach (var cell in base.AnimationCells(actor)) yield return cell;
		if (destination.HasValue)
			foreach (var cell in AnimationPath(actor, destination.Value)) yield return cell;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}
