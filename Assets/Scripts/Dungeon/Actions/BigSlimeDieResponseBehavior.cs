using UnityEngine;

public class BigSlimeDieResponseBehavior : MonoBehaviour, IResponseBehavior
{
	public Enemy EnemyToSpawnPrefab;
	public int SlimesToSpawn;

	public GameActionResponse GetActionResponse(GameAction gameAction)
	{
		return new BigSlimeDieResponse(EnemyToSpawnPrefab, SlimesToSpawn);
	}
}
