using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Dungeon : MonoBehaviour
{
    public Tilemap TileMap;
    public Tilemap TileMap_Floor;
    public Tilemap ObjectTileMap;

	private int dungeonWidth;
	private int dungeonHeight;

	public BSPNode BSPRoot { get; internal set; }

	internal void InitializeGrid(int dungeonWidth, int dungeonHeight)
    {
		this.dungeonWidth = dungeonWidth;
		this.dungeonHeight = dungeonHeight;

		TileMap.ClearAllTiles();
        TileMap_Floor.ClearAllTiles();
        ObjectTileMap.ClearAllTiles();
    }

	internal Vector3Int GetRandomEnemyPosition()
	{
		var rooms = BSPNode.Flatten(BSPRoot)
				.Where(x => x != null)
				.Where(x => x.room != null)
				.ToList();

		var randomRoom = rooms[UnityEngine.Random.Range(0, rooms.Count())];

		List<Vector3Int> validPositions = new();
		for (int i = 0; i < randomRoom.room.Width; i++)
		{
			for (int j = 0; j < randomRoom.room.Height; j++)
			{
				var tile = TileMap_Floor.GetTile(new Vector3Int(randomRoom.room.X + i, randomRoom.room.Y + j));
				if (tile != null)
				{
					validPositions.Add(new Vector3Int(randomRoom.room.X + i, randomRoom.room.Y + j));
				}
			}
		}

		var finalPosition = validPositions[UnityEngine.Random.Range(0, validPositions.Count())];

		return finalPosition;
	}

	internal Vector3Int? GetDropPosition(Vector3Int startPosition)
	{
		BFS.Node[,] grid = new BFS.Node[dungeonWidth, dungeonHeight];

		for (int i = 0; i < dungeonWidth; i++)
		{
			for (int j = 0; j < dungeonHeight; j++)
			{
				var tile = TileMap_Floor.GetTile(new Vector3Int(i, j));
				var isWalkable = tile != null;

				if (isWalkable)
				{
					grid[i, j] = new BFS.Node(i, j);
				}
			}
		}
		BFS.Node startNode = grid[startPosition.x, startPosition.y];

		var path = BFS.FindPath(grid,
			startNode,
			(node) =>
			{
				Vector3Int position = new Vector3Int(node.X, node.Y);
				var tile = ObjectTileMap.GetTile(position);
				return tile == null;
			});

		if (path == null)
		{
			return null;
		}

		BFS.Node node = path.Last();
		return new Vector3Int(node.X, node.Y);
	}
	internal bool CanWalk(Vector3Int newMapPosition)
	{
		var tile = TileMap_Floor.GetTile(newMapPosition);
		return tile != null;
	}

	internal void SetTreasure(Vector3Int treasurePosition, TreasureTile treasureTile)
	{
		var newTreasureTile = treasureTile.CloneTile();
		newTreasureTile.OpenedAction = () =>
		{
			//give treasure to player
			int goldAmount = UnityEngine.Random.Range(3, 10);
			Game game = Game.Instance;
			Player playerCharacter = game.PlayerCharacter;
			playerCharacter.Vitals.Gold += goldAmount;
			playerCharacter.SyncDisplayedStats();
			game.DoFloatingText($"{goldAmount} Gold", Color.yellow, playerCharacter.transform.position);
		};
		ObjectTileMap.SetTile(treasurePosition, newTreasureTile);
		//ObjectTileMap.SetTile(treasurePosition, TreasureTile);
	}

	internal bool CanWalkTo(Vector3Int origin, Vector3Int destination)
	{
		var validWalkDirections = GetValidWalkDirections(origin);
		var validWalkPositions = validWalkDirections.Select(x => origin + GetFacingOffset(x));

		return validWalkPositions.Contains(destination);
	}

	internal List<Facing> GetValidWalkDirections(Vector3Int tilemapPosition)
	{
		var dungeonGenerator = Game.Instance.DungeonGenerator;
		List<Facing> ret = new();

		if (CanWalk(tilemapPosition + GetFacingOffset(Facing.Left)) &&
			CanWalk(tilemapPosition + GetFacingOffset(Facing.Down)) &&
			CanWalk(tilemapPosition + GetFacingOffset(Facing.DownLeft)))
		{
			ret.Add(Facing.DownLeft);
		}
		if (CanWalk(tilemapPosition + GetFacingOffset(Facing.Down)))
		{
			ret.Add(Facing.Down);
		}
		if (CanWalk(tilemapPosition + GetFacingOffset(Facing.Right)) &&
			CanWalk(tilemapPosition + GetFacingOffset(Facing.Down)) &&
			CanWalk(tilemapPosition + GetFacingOffset(Facing.DownRight)))
		{
			ret.Add(Facing.DownRight);
		}
		if (CanWalk(tilemapPosition + GetFacingOffset(Facing.Left)))
		{
			ret.Add(Facing.Left);
		}
		if (CanWalk(tilemapPosition + GetFacingOffset(Facing.Right)))
		{
			ret.Add(Facing.Right);
		}
		if (CanWalk(tilemapPosition + GetFacingOffset(Facing.Left)) &&
			CanWalk(tilemapPosition + GetFacingOffset(Facing.Up)) &&
			CanWalk(tilemapPosition + GetFacingOffset(Facing.UpLeft)))
		{
			ret.Add(Facing.UpLeft);
		}
		if (CanWalk(tilemapPosition + GetFacingOffset(Facing.Up)))
		{
			ret.Add(Facing.Up);
		}
		if (CanWalk(tilemapPosition + GetFacingOffset(Facing.Right)) &&
			CanWalk(tilemapPosition + GetFacingOffset(Facing.Up)) &&
			CanWalk(tilemapPosition + GetFacingOffset(Facing.UpRight)))
		{
			ret.Add(Facing.UpRight);
		}

		return ret;
	}

	internal Vector3Int GetRangedAttackPosition(
		Character thrower,
		Vector3Int origin,
		Facing direction,
		int maxRange,
		Func<Vector3Int, Vector3Int, Character, bool> stopCondition)
	{
		var game = Game.Instance;
		var offset = GetFacingOffset(direction);

		var currentPosition = origin;

		// Iterate within the specified range in the given direction
		for (int distance = 1; distance <= maxRange; distance++)
		{
			// Calculate the target position based on origin, direction, and distance
			var nextPosition = new Vector3Int(
				origin.x + offset.x * distance,
				origin.y + offset.y * distance,
				origin.z + offset.z * distance
			);

			if (stopCondition.Invoke(currentPosition, nextPosition, thrower))
			{
				return currentPosition;
			}

			currentPosition = nextPosition;
		}

		//range expired
		return currentPosition;
	}

	internal List<Facing> GetValidAttackDirections(Vector3Int tilemapPosition)
	{
		List<Facing> ret = new();

		if (CanWalk(tilemapPosition + GetFacingOffset(Facing.Left)) &&
			CanWalk(tilemapPosition + GetFacingOffset(Facing.Down)))
		{
			ret.Add(Facing.DownLeft);
		}
		if (CanWalk(tilemapPosition + GetFacingOffset(Facing.Down)))
		{
			ret.Add(Facing.Down);
		}
		if (CanWalk(tilemapPosition + GetFacingOffset(Facing.Right)) &&
			CanWalk(tilemapPosition + GetFacingOffset(Facing.Down)))
		{
			ret.Add(Facing.DownRight);
		}
		if (CanWalk(tilemapPosition + GetFacingOffset(Facing.Left)))
		{
			ret.Add(Facing.Left);
		}
		if (CanWalk(tilemapPosition + GetFacingOffset(Facing.Right)))
		{
			ret.Add(Facing.Right);
		}
		if (CanWalk(tilemapPosition + GetFacingOffset(Facing.Left)) &&
			CanWalk(tilemapPosition + GetFacingOffset(Facing.Up)))
		{
			ret.Add(Facing.UpLeft);
		}
		if (CanWalk(tilemapPosition + GetFacingOffset(Facing.Up)))
		{
			ret.Add(Facing.Up);
		}
		if (CanWalk(tilemapPosition + GetFacingOffset(Facing.Right)) &&
			CanWalk(tilemapPosition + GetFacingOffset(Facing.Up)))
		{
			ret.Add(Facing.UpRight);
		}

		return ret;
	}

	static public Vector3Int GetFacingOffset(Facing facing)
	{
		switch (facing)
		{
			case Facing.Up:
				return new Vector3Int(0, 1, 0);
			case Facing.Down:
				return new Vector3Int(0, -1, 0);
			case Facing.Left:
				return new Vector3Int(-1, 0, 0);
			case Facing.Right:
				return new Vector3Int(1, 0, 0);
			case Facing.UpLeft:
				return new Vector3Int(-1, 1, 0);
			case Facing.UpRight:
				return new Vector3Int(1, 1, 0);
			case Facing.DownLeft:
				return new Vector3Int(-1, -1, 0);
			case Facing.DownRight:
				return new Vector3Int(1, -1, 0);
		}

		//this should never happen
		return new Vector3Int(0, 0, 0);
	}

	internal void SetDroppedItem(Vector3Int treasurePosition, ItemDefinition item, DroppedItemTile droppedItemTile, int? stock = null)
	{
		var newDroppedItemTile = droppedItemTile.CloneTile();
		newDroppedItemTile.ItemDefinition = item;
		newDroppedItemTile.OpenedAction = () =>
		{
			//give treasure to player
			Game game = Game.Instance;
			game.PlayerCharacter.Inventory.Add(Common.Instance.ItemManager.GetAsInventoryItem(item, stock));

			ObjectTileMap.SetTile(treasurePosition, null);
		};
		ObjectTileMap.SetTile(treasurePosition, newDroppedItemTile);
	}
	internal bool IsExit(Vector3Int newMapPosition)
	{
		var tile = ObjectTileMap.GetTile(newMapPosition);

		if (tile is InteractableTile interactableTile)
		{
			return interactableTile.IsStair;
		}

		return false;
	}

	internal InteractableTile GetInteractable(Vector3Int mapPosition)
	{
		var tile = ObjectTileMap.GetTile(mapPosition);

		return tile as InteractableTile;
	}

	public static bool StopArrow(Vector3Int currentPosition, Vector3Int nextPosition, Character thrower)
	{
		Character target = Game.Instance.AllCharacters
			.Where(x => x != thrower)
			.FirstOrDefault(x => x.TilemapPosition == currentPosition);

		if (target != null) { return true; }

		bool hitWall = !Game.Instance.CurrentDungeon.IsWalkable(nextPosition);
		if (hitWall) { return true; }

		return false;
	}
}
