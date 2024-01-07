public class CastSleepPolicyOverride : PolicyOverride
{
	public StatusEffect statusEffectPrefab;

	public override PolicyBase GetOverridePolicy(Game game, Enemy enemy)
	{
		return new CastSleepPolicy(game, enemy, Priority, statusEffectPrefab);
	}
}
