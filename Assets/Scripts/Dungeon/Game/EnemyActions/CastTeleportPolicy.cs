using System.Collections.Generic;

internal class CastTeleportPolicy : PolicyBase
{
	public CastTeleportPolicy(Game game, Character enemy, int priority) : base(game, enemy, priority)
	{
	}

	public override List<GameAction> GetActions()
	{
		return new List<GameAction>()
		{
			new CastSpellAction()
			{
				GetActionsFunc = () =>
				{
					return new()
					{
						new WarpAction(character.PursuitTarget)
					};
				}
			}
		};
	}

	public override bool ShouldRun()
	{
		return AttackPolicy.CanAttack(game, character.PursuitTarget, character) &&
			UnityEngine.Random.value > 0.5f;
	}
}