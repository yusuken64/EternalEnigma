using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class SwapAllyPositionAction : GameAction
{
	private Ally ally;
	private Ally swapAlly;
    internal Ally SwappedAlly => swapAlly;
    private bool blocked;

	private Vector3Int originalPosition;
	private Vector3Int newMapPosition;
	public SwapAllyPositionAction()
	{

	}
	public SwapAllyPositionAction(Ally ally, Character swapAlly)
	{
		this.ally = ally;
		this.swapAlly = swapAlly as Ally;
		originalPosition = ally.TilemapPosition;
		newMapPosition = swapAlly.TilemapPosition;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
        blocked=(ally != null && ally.IsMovementBlocked) || (swapAlly != null && swapAlly.IsMovementBlocked);
        if(blocked) return new();
		ally.TilemapPosition = newMapPosition;
		swapAlly.TilemapPosition = originalPosition;
		return new();
	}

	internal override void RecordOutcome(Character character)
	{
		if (blocked) GameMessages.ForCharacter(character, $"{GameMessages.Name(character)} can't move!");
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
        if(blocked)
        {
            ally?.HeroAnimator?.StopWalkContinuation();
            swapAlly?.HeroAnimator?.StopWalkContinuation();
            yield break;
        }
		var worldPosition = Game.Instance.CurrentDungeon.CellToWorld(newMapPosition);
		var worldPosition2 = Game.Instance.CurrentDungeon.CellToWorld(originalPosition);

        if (skipAnimation)
        {
            character.transform.position = worldPosition;
            swapAlly.transform.position = worldPosition2;
            character.PlayIdleAnimation();
            swapAlly.PlayIdleAnimation();
            yield break;
        }
		bool continuous = Game.Instance.PlayerController?.CanContinueHeldWalk == true;
		MovementAction.BeginWalk(character, continuous);
		MovementAction.BeginWalk(swapAlly, continuous);

		Sequence moveSequence = DOTween.Sequence();
		var first = character.transform.DOMove(worldPosition, 0.1f / character.FinalStats.ActionsPerTurnMax);
		var second = swapAlly.transform.DOMove(worldPosition2, 0.1f / character.FinalStats.ActionsPerTurnMax);
		if (continuous) { first.SetEase(Ease.Linear); second.SetEase(Ease.Linear); }
		moveSequence.Join(first);
		moveSequence.Join(second);

		yield return moveSequence.WaitForCompletion();

		bool retain = continuous && Game.Instance.PlayerController?.CanContinueHeldWalk == true;
		MovementAction.CompleteWalk(character, retain);
		MovementAction.CompleteWalk(swapAlly, retain);
	}

    internal override void AddDestinationSight(HashSet<Vector3Int> tiles)
    {
        AddAllySight(tiles, ally, newMapPosition); AddAllySight(tiles, swapAlly, originalPosition);
    }

    internal override IEnumerable<Vector3Int> AnimationCells(Character actor)
    {
        foreach (var cell in base.AnimationCells(actor)) yield return cell;
        foreach (var cell in AnimationPath(actor, newMapPosition)) yield return cell;
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
