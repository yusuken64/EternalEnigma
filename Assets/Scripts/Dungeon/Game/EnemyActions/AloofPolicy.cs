using System.Collections.Generic;
using System.Linq;

public class AloofPolicy : PolicyBase
{
	public AloofPolicy(Game game, Character enemy, int priority) : base(game, enemy, priority) { }

	public override List<GameAction> GetActions()
	{
		var validWalkDirections = game.CurrentDungeon.GetValidWalkDirections(character.TilemapPosition);

		if (validWalkDirections.Any())
		{
			character.CurrentFacing = validWalkDirections[UnityEngine.Random.Range(0, validWalkDirections.Count())];
		}
		return new WanderPolicy(game, character, priority).GetActions();
	}

	public override bool ShouldRun()
	{
		return UnityEngine.Random.value < 0.5f;
	}
}