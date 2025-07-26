using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class OverworldMovement : OverworldAction
{
	private OverworldPlayer overworldPlayer;
	private Vector3Int originalPosition;
	private Vector3Int newMapPosition;

	public OverworldMovement(OverworldPlayer overworldPlayer, Vector3Int originalPosition, Vector3Int newMapPosition)
	{
		this.overworldPlayer = overworldPlayer;
		this.originalPosition = originalPosition;
		this.newMapPosition = newMapPosition;
	}

	internal override List<OverworldAction> ExecuteImmediate()
	{
		// Move the currently controlled ally
		overworldPlayer.ControllingOverworldAlly.TilemapPosition = newMapPosition;
		overworldPlayer.RecordWalkPosition();

		int trailIndex = 1;
		foreach (var ally in overworldPlayer.RecruitedAllies)
		{
			if (ally == overworldPlayer.ControllingOverworldAlly)
				continue;

			// Move following ally to previous position of their leader
			ally.TilemapPosition = overworldPlayer.GetNthFromLastPosition(trailIndex);
			trailIndex++;
		}

		return new();
	}

	internal override IEnumerator ExecuteRoutine()
	{
		// Reorder list so the controlling ally is first
		List<OverworldAlly> orderedAllies = new();
		orderedAllies.Add(overworldPlayer.ControllingOverworldAlly);
		orderedAllies.AddRange(overworldPlayer.RecruitedAllies.Where(a => a != overworldPlayer.ControllingOverworldAlly));

		List<Tweener> tweens = new();

		for (int i = 0; i < orderedAllies.Count; i++)
		{
			var ally = orderedAllies[i];
			var targetTile = overworldPlayer.GetNthFromLastPosition(i);
			Vector3 targetWorld = overworldPlayer.WalkableMap.CellToWorld(targetTile);

			// Calculate facing based on current world position (not TilemapPosition)
			Vector3 offsetWorld = targetWorld - ally.transform.position;
			var direction = new Vector3Int((int)Mathf.Clamp(offsetWorld.x, -1, 1),
					  (int)Mathf.Clamp(offsetWorld.y, -1, 1),
					  (int)offsetWorld.z);
			ally.SetFacing(GetFacing(direction));

			ally.HeroAnimator.PlayWalkAnimation();
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
			ally.HeroAnimator.PlayIdleAnimation();
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

	internal OverworldMovement GetReverse()
	{
		return new OverworldMovement(this.overworldPlayer, newMapPosition, originalPosition);
	}
}

public abstract class OverworldAction
{
	internal abstract List<OverworldAction> ExecuteImmediate();
	internal abstract IEnumerator ExecuteRoutine();
}