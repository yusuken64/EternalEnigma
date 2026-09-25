using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class TeleportBehindAction : GameAction
{
	public int MaxRange = 4;
	public float DamagePercent = 1f;

	private Character caster;
	private Character target;
	private SkillRankContext rank;
	private Vector3Int? destination;

	public TeleportBehindAction() { }

	private static TakeDamageAction Strike(Character caster, Character target, float percent, SkillRankContext rank)
	{
		AttackAction.GetAttackDamage(caster, target, out bool hit, out int damage);
		int scaled = rank.Scaling.ScalePower(Mathf.RoundToInt(damage * percent), rank.Rank);
		return new TakeDamageAction(caster, target, Math.Max(0, scaled), true, !hit);
	}

	internal override GameAction AsTargetedSkill(Character caster, Character target)
	{
		return AsTargetedSkill(caster, target, SkillRankContext.Unranked);
	}

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank)
	{
		var bound = new TeleportBehindAction
		{
			MaxRange = this.MaxRange,
			DamagePercent = this.DamagePercent,
			caster = caster,
			target = target,
			rank = rank,
			destination = null
		};
		return bound;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		destination = null;
		if (caster == null || target == null || target.Vitals.HP <= 0)
			return new();

		if (TileWorldDungeon.ChevDistance(caster.TilemapPosition, target.TilemapPosition) > MaxRange)
			return new();

		var tile = SkillMovement.FindTileBehind(caster, target);
		if (tile == null)
			return new();

		SkillMovement.Place(caster, tile.Value);
		destination = tile;
		TrackAnimationTarget(target);

		return new List<GameAction> { Strike(caster, target, DamagePercent, rank) };
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (destination == null)
			yield break;

		if (skipAnimation)
		{
			SkillMovement.SnapTo(caster, destination.Value);
			yield break;
		}

		yield return SkillMovement.AnimateMove(caster, destination.Value);
		caster.SetFacingByTargetPosition(target.TilemapPosition);
	}

	internal override void AddDestinationSight(HashSet<Vector3Int> tiles)
	{
		if (destination != null)
			AddAllySight(tiles, caster, destination.Value);
	}

	internal override IEnumerable<Vector3Int> AnimationCells(Character actor)
	{
		foreach (var cell in base.AnimationCells(actor))
			yield return cell;
		if (destination != null)
			foreach (var cell in AnimationPath(actor, destination.Value))
				yield return cell;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}
