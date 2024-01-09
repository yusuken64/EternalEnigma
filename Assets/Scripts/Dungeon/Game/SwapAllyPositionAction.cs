using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class SwapAllyPositionAction : GameAction
{
	private Player player;
	private Ally swapAlly;

	private Vector3Int originalPosition;
	private Vector3Int newMapPosition;

	public SwapAllyPositionAction(Player player, Character swapAlly)
	{
		this.player = player;
		this.swapAlly = swapAlly as Ally;
		originalPosition = player.TilemapPosition;
		newMapPosition = swapAlly.TilemapPosition;
		this.swapAlly.SetAction(new MovementAction(swapAlly, newMapPosition, originalPosition));
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		player.TilemapPosition = newMapPosition;
		return new();
	}


	internal override IEnumerator ExecuteRoutine(Character character)
	{
		var worldPosition = Game.Instance.CurrentDungeon.CellToWorld(newMapPosition);

		character.PlayWalkAnimation();
		yield return character.transform.DOMove(worldPosition, 0.1f / character.FinalStats.ActionsPerTurnMax)
			.WaitForCompletion();

		character.PlayIdleAnimation();
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}

	internal override bool CanBeCombined(GameAction action)
	{
		return action is MovementAction ||
			   action is SwapAllyPositionAction;
	}
}