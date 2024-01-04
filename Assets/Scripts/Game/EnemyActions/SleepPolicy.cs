using System.Collections.Generic;

internal class SleepPolicy : PolicyBase
{
	private StatusEffect statusEffectPrefab;

	public SleepPolicy(Game game, Enemy enemy, int priority, StatusEffect statusEffectPrefab) : base(game, enemy, priority)
	{
		this.statusEffectPrefab = statusEffectPrefab;
	}

	public override List<GameAction> GetActions()
	{
		enemy.SetFacingByTargetPosition(game.PlayerCharacter.TilemapPosition); //TODO add this to action?
		return new List<GameAction>() { new ApplyStatusEffectAction(game.PlayerCharacter, statusEffectPrefab) };
	}

	public override bool ShouldRun()
	{
		return AttackPolicy.CanAttack(game, game.PlayerCharacter, enemy) &&
			UnityEngine.Random.value > 0.5f;
	}
}