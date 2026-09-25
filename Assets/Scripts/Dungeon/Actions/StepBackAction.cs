using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class StepBackAction : GameAction
{
	public int Tiles = 1;

	private Character caster;
	private Character target;
	private Vector3Int? destination;

	public StepBackAction() { }

	internal override GameAction AsTargetedSkill(Character caster, Character target)
	{
		return AsTargetedSkill(caster, target, SkillRankContext.Unranked);
	}

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank)
	{
		var bound = new StepBackAction
		{
			Tiles = this.Tiles,
			caster = caster,
			target = target
		};
		return bound;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (caster == null || target == null)
			return new();

		var dir = SkillMovement.Step(target.TilemapPosition, caster.TilemapPosition);
		if (dir == Vector3Int.zero)
			return new();

		var cell = SkillMovement.LastFreeAlong(caster, dir, Tiles);
		if (cell != caster.TilemapPosition)
		{
			SkillMovement.Place(caster, cell);
			destination = cell;
		}

		return new();
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
