public class CastFrailPolicyOverride : PolicyOverride
{
	public StatusEffect statusEffectPrefab;

	public override PolicyBase GetOverridePolicy(Game game, Enemy enemy)
	{
		return new CastFrailPolicy(game, enemy, Priority, statusEffectPrefab);
	}
}
