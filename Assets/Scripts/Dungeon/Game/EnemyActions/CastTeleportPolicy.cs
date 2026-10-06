using System.Collections.Generic;

internal class CastTeleportPolicy : PolicyBase
{
	public CastTeleportPolicy(Game game, Character enemy, int priority) : base(game, enemy, priority)
	{
	}

	public override List<GameAction> GetActions()
	{
        var originalTarget = character.PursuitTarget;
		return new List<GameAction>()
		{
			new CastSpellAction()
			{
                SpellName = "Teleport", Target = originalTarget,
				CastSound = AudioManager.Instance?.SoundEffects.Teleport,
				GetActionsFunc = () =>
				{
					return new()
					{
						new WarpAction(originalTarget)
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
