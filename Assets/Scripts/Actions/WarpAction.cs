using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class WarpAction : GameAction
{
	private Character attacker;
	private Vector3Int warpLoccation;

	public WarpAction(Character attacker)
	{
		this.attacker = attacker;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		var game = Game.Instance;
		warpLoccation = game.CurrentDungeon.GetRandomEnemyPosition();

		attacker.TilemapPosition = warpLoccation;

		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		var worldPosition = Game.Instance.CurrentDungeon.TileMap_Floor.CellToWorld(warpLoccation);

		attacker.PlayWalkAnimation();
		yield return attacker.transform.DOMove(worldPosition, 0.1f)
			.WaitForCompletion();

		attacker.PlayIdleAnimation();
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}