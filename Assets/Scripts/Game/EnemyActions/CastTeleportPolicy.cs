using System.Collections.Generic;

internal class CastTeleportPolicy : PolicyBase
{
	public CastTeleportPolicy(Game game, Enemy enemy, int priority) : base(game, enemy, priority)
	{
	}

	public override List<GameAction> GetActions()
	{
		return new()
		{
			new WarpAction(game.PlayerCharacter)
		};
	}

	public override bool ShouldRun()
	{
		return AttackPolicy.CanAttack(game, game.PlayerCharacter, enemy) &&
			UnityEngine.Random.value > 0.5f;
	}
}