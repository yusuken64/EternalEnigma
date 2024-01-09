public class CastFrailPolicyOverride : PolicyOverride
{
	public StatusEffect statusEffectPrefab;

	public override PolicyBase GetOverridePolicy(Game game, Character enemy)
	{
		return new CastFrailPolicy(game, enemy, Priority, statusEffectPrefab);
	}
}
