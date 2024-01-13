using DG.Tweening;
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
		return new();
	}

	internal override IEnumerator ExecuteRoutine()
	{
		var worldPosition = overworldPlayer.WalkableMap.CellToWorld(newMapPosition);

		overworldPlayer.PlayWalkAnimation();
		yield return overworldPlayer.transform.DOMove(worldPosition, 0.2f)
			.WaitForCompletion();

		overworldPlayer.PlayIdleAnimation();
	}
}

internal abstract class OverworldAction
{
	internal abstract List<OverworldAction> ExecuteImmediate();
	internal abstract IEnumerator ExecuteRoutine();
}