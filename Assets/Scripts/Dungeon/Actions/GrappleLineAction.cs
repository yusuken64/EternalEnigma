using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Moves only over walkable dungeon tiles; this is not the Grapple capability and never satisfies capability locks.
/// </summary>
[Serializable]
public class GrappleLineAction : GameAction
{
	public int MaxRange = 5;

	private Character caster;
	private Vector3Int? destination;

	public GrappleLineAction() { }

	internal override GameAction AsTargetedSkill(Character caster, Character target)
	{
		return AsTargetedSkill(caster, target, SkillRankContext.Unranked);
	}

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank)
	{
		var bound = new GrappleLineAction
		{
			MaxRange = this.MaxRange,
			caster = caster
		};
		return bound;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (caster == null)
			return new();

		var dir = TileWorldDungeon.GetFacingOffset(caster.CurrentFacing);
		var cell = SkillMovement.LastFreeAlong(caster, new Vector3Int(dir.x, dir.y, 0), MaxRange);
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
