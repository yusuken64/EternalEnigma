using System.Collections.Generic;
using System.Linq;
using UnityEngine;

//split into two when attacked 50% of the time
public class SpiderActionResponse : GameActionResponse
{
	private Enemy enemyToSpawnPrefab;

	public SpiderActionResponse(Enemy enemyToSpawnPrefab)
	{
		this.enemyToSpawnPrefab = enemyToSpawnPrefab;
	}

	public override List<GameAction> GetResponseTo(Character character, GameAction gameAction)
	{
		if (gameAction is TakeDamageAction takeDamageAction)
		{
			//TODO do this check somewhere else common to all responses?
			if (takeDamageAction.target != character ||
				character.Vitals.HP <= 0)
			{
				return new();
			}

			if (UnityEngine.Random.value < 0.5f)
			{
				return new();
			}

			var possiblePositions = Game.Instance.CurrentDungeon.GetWalkableNeighborhoodTiles(takeDamageAction.target.TilemapPosition)
				.Where(x => !Game.Instance.AllCharacters.Any(character => character.TilemapPosition == x));

			if (!possiblePositions.Any())
			{
				return new();
			}

			var spawnTilePosition = possiblePositions.Sample();
			var spawnSourceWorldLocation = takeDamageAction.target.transform.position;

			return new()
			{
				new SpawnEnemyAction(enemyToSpawnPrefab, spawnTilePosition, spawnSourceWorldLocation)
			};
		}

		return new();
	}
}
