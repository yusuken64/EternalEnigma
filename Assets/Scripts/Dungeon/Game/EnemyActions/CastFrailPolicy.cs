using System.Collections.Generic;

internal class CastFrailPolicy : PolicyBase
{
	private StatusEffect statusEffectPrefab;

	public CastFrailPolicy(Game game, Enemy enemy, int priority, StatusEffect statusEffectPrefab) : base(game, enemy, priority)
	{
		this.statusEffectPrefab = statusEffectPrefab;
	}

	public override List<GameAction> GetActions()
	{
		enemy.SetFacingByTargetPosition(game.PlayerCharacter.TilemapPosition);
		return new List<GameAction>() { new ApplyStatusEffectAction(game.PlayerCharacter, statusEffectPrefab, enemy) };
	}

	public override bool ShouldRun()
	{
		return AttackPolicy.CanAttack(game, game.PlayerCharacter, enemy) &&
			UnityEngine.Random.value > 0.8f;
	}
}
