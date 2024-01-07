public class SpiderActionResponseBehavior : ResponseBehavior
{
	public Enemy EnemyToSpawnPrefab;

	public override GameActionResponse GetActionResponse(GameAction gameAction)
	{
		return new SpiderActionResponse(EnemyToSpawnPrefab);
	}
}
