using UnityEngine;

public class SpiderActionResponseBehavior : MonoBehaviour, IResponseBehavior
{
	public Enemy EnemyToSpawnPrefab;

	public GameActionResponse GetActionResponse(GameAction gameAction)
	{
		return new SpiderActionResponse(EnemyToSpawnPrefab);
	}
}
