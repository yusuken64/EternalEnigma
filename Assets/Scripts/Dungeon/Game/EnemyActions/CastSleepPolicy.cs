using System.Collections.Generic;

internal class CastSleepPolicy : PolicyBase
{
	private StatusEffect statusEffectPrefab;

	public CastSleepPolicy(Game game, Enemy enemy, int priority, StatusEffect statusEffectPrefab) : base(game, enemy, priority)
	{
		this.statusEffectPrefab = statusEffectPrefab;
	}

	public override List<GameAction> GetActions()
	{
		return new List<GameAction>()
		{
			new CastSpellAction()
			{
				GetActionsFunc = () =>
				{
					enemy.SetFacingByTargetPosition(game.PlayerCharacter.TilemapPosition);
					return new List<GameAction>() { new ApplyStatusEffectAction(game.PlayerCharacter, statusEffectPrefab, enemy) };
				}
			}
		};
	}

	public override bool ShouldRun()
	{
		return AttackPolicy.CanAttack(game, game.PlayerCharacter, enemy) &&
			UnityEngine.Random.value > 0.8f;
	}
}
