using System.Collections.Generic;

internal class CastSleepPolicy : PolicyBase
{
	private StatusEffect statusEffectPrefab;

	public CastSleepPolicy(Game game, Character enemy, int priority, StatusEffect statusEffectPrefab) : base(game, enemy, priority)
	{
		this.statusEffectPrefab = statusEffectPrefab;
	}

	public override List<GameAction> GetActions()
	{
        var originalTarget = character.PursuitTarget;
		return new List<GameAction>()
		{
			new CastSpellAction()
			{
                SpellName = "Sleep", Target = originalTarget,
				CastSound = AudioManager.Instance?.SoundEffects.Sleep,
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
		return AttackPolicy.CanAttack(game, character.PursuitTarget, character) &&
			UnityEngine.Random.value > 0.8f;
	}
}
