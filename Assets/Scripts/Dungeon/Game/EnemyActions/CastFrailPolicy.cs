using System.Collections.Generic;

internal class CastFrailPolicy : PolicyBase
{
	private StatusEffect statusEffectPrefab;

	public CastFrailPolicy(Game game, Character enemy, int priority, StatusEffect statusEffectPrefab) : base(game, enemy, priority)
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
					character.SetFacingByTargetPosition(game.PlayerCharacter.TilemapPosition);
					return new List<GameAction>() { new ApplyStatusEffectAction(game.PlayerCharacter, statusEffectPrefab, character) };
				}
			}
		};
	}

	public override bool ShouldRun()
	{
		return AttackPolicy.CanAttack(game, game.PlayerCharacter, character) &&
			UnityEngine.Random.value > 0.8f;
	}
}
