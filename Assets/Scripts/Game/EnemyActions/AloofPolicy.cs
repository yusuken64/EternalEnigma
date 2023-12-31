using System.Collections.Generic;
using System.Linq;

public class AloofPolicy : PolicyBase
{
	public AloofPolicy(Game game, Enemy enemy, int priority) : base(game, enemy, priority) { }

	public override List<GameAction> GetActions()
	{
		var validWalkDirections = game.CurrentDungeon.GetValidWalkDirections(enemy.TilemapPosition);

		if (validWalkDirections.Any())
		{
			enemy.CurrentFacing = validWalkDirections[UnityEngine.Random.Range(0, validWalkDirections.Count())];
		}
		return new WanderPolicy(game, enemy, priority).GetActions();
	}

	public override bool ShouldRun()
	{
		return UnityEngine.Random.value < 0.5f;
	}
}