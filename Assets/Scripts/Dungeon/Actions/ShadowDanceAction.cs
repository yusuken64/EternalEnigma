using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class ShadowDanceAction : GameAction
{
	public int Radius = 2;
	public float DamagePercent = 1f;

	private Character caster;
	private SkillRankContext rank;
	private readonly List<(Vector3Int cell, Character target)> steps = new();

	public ShadowDanceAction() { }

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
		var bound = new ShadowDanceAction
		{
			Radius = this.Radius,
			DamagePercent = this.DamagePercent,
			caster = caster,
			rank = rank
		};
		return bound;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		steps.Clear();
		if (caster == null)
			return new();

		var origin = caster.TilemapPosition;

		var enemies = Game.Instance.Enemies
			.Where(e => e != null && e.Team != caster.Team && e.Vitals.HP > 0 &&
				TileWorldDungeon.ChevDistance(e.TilemapPosition, origin) <= Radius)
			.OrderBy(e => TileWorldDungeon.ChevDistance(e.TilemapPosition, origin))
			.ThenBy(e => e.TilemapPosition.x)
			.ThenBy(e => e.TilemapPosition.y)
			.ToList();

		List<GameAction> result = new();

		foreach (var enemy in enemies)
		{
			var tile = SkillMovement.FindTileBehind(caster, enemy);
			if (tile == null)
				continue;

			SkillMovement.Place(caster, tile.Value);
			steps.Add((tile.Value, enemy));
			TrackAnimationTarget(enemy);
			result.Add(Strike(caster, enemy, DamagePercent, rank));
		}

		return result;
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (steps.Count == 0)
			yield break;

		if (skipAnimation)
		{
			SkillMovement.SnapTo(caster, steps[^1].cell);
			yield break;
		}

		foreach (var (cell, target) in steps)
		{
			yield return SkillMovement.AnimateMove(caster, cell, 0.08f);
			if (target != null)
				caster.SetFacingByTargetPosition(target.TilemapPosition);
		}
	}

	internal override void AddDestinationSight(HashSet<Vector3Int> tiles)
	{
		foreach (var (cell, _) in steps)
			AddAllySight(tiles, caster, cell);
	}

	internal override IEnumerable<Vector3Int> AnimationCells(Character actor)
	{
		foreach (var cell in base.AnimationCells(actor))
			yield return cell;
		foreach (var (cell, _) in steps)
			foreach (var pathCell in AnimationPath(actor, cell))
				yield return pathCell;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}
