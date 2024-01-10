using System.Collections.Generic;
using System.Linq;

public class BigSlimeDieResponse : GameActionResponse
{
	private Enemy enemyToSpawnPrefab;
	private readonly int slimesToSpawn;

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

			for (int i = 0; i < slimesToSpawn; i++)
			{
				var possiblePositions = Game.Instance.CurrentDungeon.GetWalkableNeighborhoodTiles(deathDamageAction.target.TilemapPosition)
					.Where(x => !Game.Instance.AllCharacters.Any(character => character.TilemapPosition == x));

				if (!possiblePositions.Any())
				{
					return new();
				}

				var spawnTilePosition = possiblePositions.Sample();
				var spawnSourceWorldLocation = deathDamageAction.target.transform.position;

				ret.Add(new SpawnEnemyAction(enemyToSpawnPrefab, spawnTilePosition, spawnSourceWorldLocation));
			}

			return ret;
		}

		return new();
	}
}
