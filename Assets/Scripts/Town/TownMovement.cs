using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class TownMovement : TownAction
{
	private TownPlayer townPlayer;
	private Vector3Int originalPosition;
	private Vector3Int newMapPosition;

	public TownMovement(TownPlayer townPlayer, Vector3Int originalPosition, Vector3Int newMapPosition)
	{
		this.townPlayer = townPlayer;
		this.originalPosition = originalPosition;
		this.newMapPosition = newMapPosition;
	}

	internal override List<TownAction> ExecuteImmediate()
	{
		// Move the currently controlled ally
		townPlayer.ControllingTownAlly.TilemapPosition = newMapPosition;
		townPlayer.RecordWalkPosition();

		int trailIndex = 1;
		foreach (var ally in townPlayer.RecruitedAllies)
		{
			if (ally == townPlayer.ControllingTownAlly)
				continue;

			// Move following ally to previous position of their leader
			ally.TilemapPosition = townPlayer.GetNthFromLastPosition(trailIndex);
			trailIndex++;
		}

		return new();
	}

	internal override IEnumerator ExecuteRoutine()
	{
		// Reorder list so the controlling ally is first
		List<TownAlly> orderedAllies = new();
		orderedAllies.Add(townPlayer.ControllingTownAlly);
		orderedAllies.AddRange(townPlayer.RecruitedAllies.Where(a => a != townPlayer.ControllingTownAlly));

		List<Tweener> tweens = new();

		for (int i = 0; i < orderedAllies.Count; i++)
		{
			var ally = orderedAllies[i];
			var targetTile = townPlayer.GetNthFromLastPosition(i);
			Vector3 targetWorld = townPlayer.WalkableMap.CellToWorld(targetTile);

			// Calculate facing based on current world position (not TilemapPosition)
			Vector3 offsetWorld = targetWorld - ally.transform.position;
			var direction = new Vector3Int((int)Mathf.Clamp(offsetWorld.x, -1, 1),
					  (int)Mathf.Clamp(offsetWorld.y, -1, 1),
					  (int)offsetWorld.z);
			ally.SetFacing(GetFacing(direction));

			ally.HeroAnimator?.PlayWalkAnimation();
			var tween = ally.transform.DOMove(targetWorld, 0.2f);
			tweens.Add(tween);
		}

		// Wait for all tweens to complete in parallel
		foreach (var tween in tweens)
		{
			yield return tween.WaitForCompletion();
		}

		// Play idle for everyone
		foreach (var ally in orderedAllies)
		{
			ally.HeroAnimator?.PlayIdleAnimation();
		}
	}

	public Facing GetFacing(Vector3Int direction)
	{
		direction = new Vector3Int(Mathf.Clamp(direction.x, -1, 1),
							  Mathf.Clamp(direction.y, -1, 1),
							  direction.z);

		if (direction == Vector3Int.up)
		{
			return Facing.Up;
		}
		else if (direction == Vector3Int.down)
		{
			return Facing.Down;
		}
		else if (direction == Vector3Int.left)
		{
			return Facing.Left;
		}
		else if (direction == Vector3Int.right)
		{
			return Facing.Right;
		}
		else if (direction == new Vector3Int(-1, 1, 0))
		{
			return Facing.UpLeft;
		}
		else if (direction == new Vector3Int(1, 1, 0))
		{
			return Facing.UpRight;
		}
		else if (direction == new Vector3Int(-1, -1, 0))
		{
			return Facing.DownLeft;
		}
		else if (direction == new Vector3Int(1, -1, 0))
		{
			return Facing.DownRight;
		}
		else
		{
			// Return a default facing or handle an unknown direction
			return Facing.Up; // Change this default return as needed
		}
	}

	internal TownMovement GetReverse()
	{
		return new TownMovement(this.townPlayer, newMapPosition, originalPosition);
	}
}

public abstract class TownAction
{
	internal abstract List<TownAction> ExecuteImmediate();
	internal abstract IEnumerator ExecuteRoutine();
}
