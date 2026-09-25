using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FearStatusEffect : StatusEffect
{
	internal override string GetEffectName() => "Fear";

	internal override bool PreventsMenu() => false;

	internal override StatModification GetStatModification() => null;

	public override void Tick()
	{
		base.Tick();
	}

	internal override bool Interupts(GameAction action)
	{
		return action is AttackAction || action is RangedAttackAction || action is SkillAction || action is CastSpellAction;
	}

	public override GameAction GetActionOverride(Character character)
	{
		if (character.Team != Team.Enemy)
		{
			return null;
		}

		// Find the nearest living player-team character
		var threat = Game.Instance.AllCharacters
			.Where(c => c.Team == Team.Player && c.Vitals.HP > 0)
			.OrderBy(c => TileWorldDungeon.ChevDistance(c.TilemapPosition, character.TilemapPosition))
			.FirstOrDefault();

		if (threat == null)
		{
			return new WaitAction();
		}

		int currentDistance = TileWorldDungeon.ChevDistance(character.TilemapPosition, threat.TilemapPosition);
		MovementAction bestMove = null;
		Vector3Int bestNext = character.TilemapPosition;
		int bestDistance = currentDistance;

		// Try all facing directions
		foreach (Facing facing in System.Enum.GetValues(typeof(Facing)))
		{
			Vector3Int next = character.TilemapPosition + GridMovement.GetFacingOffset(facing);
			var move = new MovementAction(character, character.TilemapPosition, next);

			if (move.IsValid(character))
			{
				int newDistance = TileWorldDungeon.ChevDistance(next, threat.TilemapPosition);
				if (newDistance > bestDistance)
				{
					bestMove = move;
					bestNext = next;
					bestDistance = newDistance;
				}
			}
		}

		if (bestMove != null)
		{
			character.SetFacingByTargetPosition(bestNext);
			return bestMove;
		}

		return new WaitAction();
	}
}
