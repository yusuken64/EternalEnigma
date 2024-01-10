using System.Collections.Generic;

public class BigUnitStuckInHallway : GameActionResponse
{
	private StatusEffect stuckEffectPrefab;

	public BigUnitStuckInHallway(StatusEffect stuckEffectPrefab)
	{
		this.stuckEffectPrefab = stuckEffectPrefab;
	}

	public override List<GameAction> GetResponseTo(Character character, GameAction gameAction)
	{
		if (gameAction is MovementAction action &&
			action.Character == character &&
			character is Enemy enemy &&
			//enemy.CurrentEnemyState == EnemyState.Pursuit &&
			character.FootPrint == FootPrint.Size3x3 &&
			Game.Instance.CurrentDungeon.IsHallway(character.TilemapPosition))
		{
			return new()
			{
				new ApplyStatusEffectAction(character, stuckEffectPrefab, character)
			};
		}

		return new();
	}
}