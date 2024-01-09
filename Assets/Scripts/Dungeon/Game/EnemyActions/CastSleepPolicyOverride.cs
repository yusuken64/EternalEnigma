public class CastSleepPolicyOverride : PolicyOverride
{
	public StatusEffect statusEffectPrefab;

	public override PolicyBase GetOverridePolicy(Game game, Character enemy)
	{
		return new CastSleepPolicy(game, enemy, Priority, statusEffectPrefab);
	}
}
