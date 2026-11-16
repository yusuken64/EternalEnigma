using System.Collections.Generic;
using System.Linq;

public class BigSlimeDieResponse : GameActionResponse
{
	private Enemy enemyToSpawnPrefab;
	private readonly int slimesToSpawn;

	public BigSlimeDieResponse() { }
	public BigSlimeDieResponse(Enemy enemyToSpawnPrefab, int slimesToSpawn)
	{
		this.enemyToSpawnPrefab = enemyToSpawnPrefab;
		this.slimesToSpawn = slimesToSpawn;
	}

	public override List<GameAction> GetResponseTo(Character character, GameAction gameAction)
	{
		if (gameAction is DeathAction deathDamageAction &&
			deathDamageAction.target == character)
		{
			var ret = new List<GameAction>();

			var reserved = new HashSet<UnityEngine.Vector3Int>();
			for (int i = 0; i < slimesToSpawn; i++)
			{
				var possiblePositions = Game.Instance.CurrentDungeon.GetWalkableNeighborhoodTiles(deathDamageAction.target.TilemapPosition)
					.Where(x => !reserved.Contains(x) && FinaleActor.AllowsPosition(character, x) && !Game.Instance.AllCharacters.Any(character => character.TilemapPosition == x));

				if (!possiblePositions.Any())
				{
					return ret;
				}

				var spawnTilePosition = possiblePositions.Sample();
                reserved.Add(spawnTilePosition);
				var spawnSourceWorldLocation = deathDamageAction.target.transform.position;

				ret.Add(new SpawnEnemyAction(enemyToSpawnPrefab, spawnTilePosition, spawnSourceWorldLocation, character));
			}

			return ret;
		}

		return new();
	}
}
