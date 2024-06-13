using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
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
		overworldPlayer.TilemapPosition = newMapPosition;
		overworldPlayer.RecordWalkPosition();
		for (int i = 0; i < overworldPlayer.RecruitedAllies.Count; i++)
		{
			OverworldAlly ally = overworldPlayer.RecruitedAllies[i];
			ally.TilemapPosition = overworldPlayer.GetNthFromLastPosition(i);
		}
		return new();
	}

	internal override IEnumerator ExecuteRoutine()
	{
		foreach(var ally in overworldPlayer.RecruitedAllies)
		{
			var offset = newMapPosition - ally.TilemapPosition;
			ally.SetFacing(GetFacing(offset));
			ally.transform.DOMove(overworldPlayer.WalkableMap.CellToWorld(ally.TilemapPosition), 0.2f);
		}

		overworldPlayer.HeroAnimator.PlayWalkAnimation();
		var worldPosition = overworldPlayer.WalkableMap.CellToWorld(newMapPosition);
		yield return overworldPlayer.transform.DOMove(worldPosition, 0.2f)
			.WaitForCompletion();

		overworldPlayer.HeroAnimator.PlayIdleAnimation();
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

internal abstract class OverworldAction
{
	internal abstract List<OverworldAction> ExecuteImmediate();
	internal abstract IEnumerator ExecuteRoutine();
}