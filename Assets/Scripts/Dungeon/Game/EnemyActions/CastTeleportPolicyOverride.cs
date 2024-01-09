public class CastTeleportPolicyOverride : PolicyOverride
{
	public override PolicyBase GetOverridePolicy(Game game, Character enemy)
	{
		return new CastTeleportPolicy(game, enemy, Priority);
	}
}
