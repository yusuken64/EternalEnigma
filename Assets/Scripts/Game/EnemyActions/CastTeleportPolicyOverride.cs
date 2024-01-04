public class CastTeleportPolicyOverride : PolicyOverride
{
	public override PolicyBase GetOverridePolicy(Game game, Enemy enemy)
	{
		return new CastTeleportPolicy(game, enemy, Priority);
	}
}
