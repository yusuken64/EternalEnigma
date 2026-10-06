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
        var originalTarget = game.PlayerController.ControlledAlly;
		return new List<GameAction>()
		{
			new CastSpellAction()
			{
                SpellName = "Frailty", Target = originalTarget,
				CastSound = AudioManager.Instance?.SoundEffects.Debuff,
				GetActionsFunc = () =>
				{
					character.SetFacingByTargetPosition(originalTarget.TilemapPosition);
					return new List<GameAction>() { new ApplyStatusEffectAction(originalTarget, statusEffectPrefab, character) };
				}
			}
		};
	}

	public override bool ShouldRun()
	{
		return AttackPolicy.CanAttack(game, game.PlayerController.ControlledAlly, character) &&
			UnityEngine.Random.value > 0.8f;
	}
}
