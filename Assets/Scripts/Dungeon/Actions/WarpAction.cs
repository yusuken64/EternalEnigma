using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class WarpAction : GameAction
{
	private Character attacker;
	private Vector3Int warpLoccation;

	public WarpAction()
	{

	}
	public WarpAction(Character attacker)
	{
		this.attacker = attacker;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (attacker == null) return new();
		warpLoccation = attacker.TilemapPosition;
		if (attacker.Vitals.HP <= 0 || attacker.IsMovementBlocked) return new();
		var game = Game.Instance;
		var reachable = DungeonPlacement.Reachable(game.CurrentDungeon, attacker.TilemapPosition);
		var cells = DungeonPlacement.OpenCells(game.CurrentDungeon, attacker)
			.Where(p => p != attacker.TilemapPosition && reachable.Contains(p) && FinaleActor.AllowsPosition(attacker, p)).ToList();
		if (cells.Count == 0) return new();
		warpLoccation = cells[Random.Range(0, cells.Count)];
		TrackAnimationTarget(attacker);
		Visuals.Configure(CombatVisualCatalog.Instance?.Warp, attacker, warpLoccation);
        Visuals.AddImpact(attacker, warpLoccation, true);
        Visuals.AfterMovement = true;
		GameMessages.ForCharacter(attacker, $"{GameMessages.Name(attacker)} warped away!");

		attacker.TilemapPosition = warpLoccation;

		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
        if (attacker == null) yield break;
        attacker.transform.position = Game.Instance.CurrentDungeon.CellToWorld(warpLoccation);
		attacker.PlayIdleAnimation();
        yield break;
	}

    internal override void AddDestinationSight(HashSet<Vector3Int> tiles)
    {
        AddAllySight(tiles, attacker, warpLoccation);
    }

    internal override IEnumerable<Vector3Int> AnimationCells(Character actor)
    {
        foreach (var cell in base.AnimationCells(actor)) yield return cell;
        foreach (var cell in AnimationPath(actor, warpLoccation)) yield return cell;
    }

	internal override bool IsValid(Character character)
	{
		return true;
	}
}
