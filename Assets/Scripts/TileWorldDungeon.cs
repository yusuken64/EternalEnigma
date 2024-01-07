using System;
using System.Collections.Generic;
using System.Linq;
using TWC;
using UnityEngine;

public class TileWorldDungeon : MonoBehaviour
{
	public string FloorLayerName;

	public List<Enemy> Enemies;
	public List<Interactable> Interactables;
	public Gold TreasurePrefab;
	public DroppedItem DroppedItemPrefab;
	public Stairs StairsPrefab;

	internal int dungeonWidth => _tileWorldCreator.twcAsset.mapWidth;
	internal int dungeonHeight => _tileWorldCreator.twcAsset.mapHeight;
	private TileWorldCreator _tileWorldCreator;
	private bool[,] _isHallwayCache;

	private void Awake()
	{
		Debug.Log("Dungeon created", this);
	}

	private void OnDestroy()
	{
		Debug.Log("Dungeon destroyed", this);
	}

	internal void Setup(TWC.TileWorldCreator tileWorldCreator)
	{
		this._tileWorldCreator = tileWorldCreator;
	}

	internal void InitializeCache()
	{
		_isHallwayCache = new bool[dungeonWidth, dungeonHeight];
		for (int i = 0; i < dungeonWidth; i++)
		{
			for (int j = 0; j < dungeonHeight; j++)
			{
				_isHallwayCache[i, j] = IsHallway(new Vector3Int(i, j, 0));
			}
		}
	}

	internal bool CanWalk(Vector3Int newMapPosition)
	{
		if (newMapPosition.x < 0 || newMapPosition.x >= dungeonWidth ||
			newMapPosition.y < 0 || newMapPosition.y >= dungeonHeight)
		{
			return false;
		}

		var floorMap = _tileWorldCreator.GetMapOutputFromBlueprintLayer(FloorLayerName);
		var floorDetected = floorMap[newMapPosition.x, newMapPosition.y];

		return floorDetected;
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

	internal BoundsInt GetVisionBounds(Vector3Int TilemapPosition)
	{
		BoundsInt visionBounds;
		if (_isHallwayCache[TilemapPosition.x, TilemapPosition.y])
		{
			visionBounds = new BoundsInt()
			{
				xMin = TilemapPosition.x - 1,
				xMax = TilemapPosition.x + 1,
				yMin = TilemapPosition.y - 1,
				yMax = TilemapPosition.y + 1
			};
		}
		else
		{
			var direction = new List<Facing>()
			{
				Facing.Up,
				Facing.Down,
				Facing.Left,
				Facing.Right,
				Facing.UpLeft,
				Facing.UpRight,
				Facing.DownLeft,
				Facing.DownRight
			};

			var walkableDirections = direction.Select(x =>
			{
				var target = GetRangedAttackPosition(null, TilemapPosition, x, 20, (x, y, z) =>
				{
					return _isHallwayCache[x.x, x.y];
				});

				return target;
			});

			visionBounds = new BoundsInt()
			{
				xMin = walkableDirections.Min(target => target.x) - 1,
				xMax = walkableDirections.Max(target => target.x) + 1,
				yMin = walkableDirections.Min(target => target.y) - 1,
				yMax = walkableDirections.Max(target => target.y) + 1
			};
		}

		return visionBounds;
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

	//Get the closest position an Item can be dropped
	public Vector3Int GetDropPosition(Vector3Int startPosition)
	{
		BFS.Node[,] grid = new BFS.Node[dungeonWidth, dungeonHeight];

		for (int i = 0; i < dungeonWidth; i++)
		{
			for (int j = 0; j < dungeonHeight; j++)
			{
				var isWalkable = CanWalk(new Vector3Int(i, j));

				if (isWalkable)
				{
					grid[i, j] = new BFS.Node(i, j);
				}
			}
		}

		BFS.Node startNode = grid[startPosition.x, startPosition.y];

		if (startNode == null)
		{
			//this is error
			return new Vector3Int(startPosition.x, startPosition.y);
		}

		var path = BFS.FindPath(grid,
			startNode,
			(node) =>
			{
				var first = Interactables.FirstOrDefault(x => x.Position == new Vector3Int(node.X, node.Y));
				return first == null;
			});

		BFS.Node node = path.Last();
		return new Vector3Int(node.X, node.Y);
	}

	internal Vector3Int GetRandomOpenEnemyPosition()
	{
		bool[,] floorMap = _tileWorldCreator.GetMapOutputFromBlueprintLayer(FloorLayerName);
		var flatMap = Flatten(floorMap, (x) => x);

		var openPosition = flatMap.Where(x => !Interactables.Any(y => y.Position == x.Coord))
			.Sample()
			.Coord;

		return openPosition;
	}

	internal List<Vector3Int> GetWalkableNeighborhoodTiles(Vector3Int tilemapPosition)
	{
		bool[,] floorMap = _tileWorldCreator.GetMapOutputFromBlueprintLayer(FloorLayerName);
		List<Vector3Int> neighborhood = new();
		for (int i = -1; i < 2; i++)
		{
			for (int j = -1; j < 2; j++)
			{
				if (floorMap[tilemapPosition.x + i, tilemapPosition.y + j])
				{
					neighborhood.Add(new Vector3Int(tilemapPosition.x + i, tilemapPosition.y + j));
				}
			}
		}

		return neighborhood;
	}

	internal int GetNeighborhoodTilesCount(Vector3Int tilemapPosition)
	{
		int count = 0;
		bool[,] floorMap = _tileWorldCreator.GetMapOutputFromBlueprintLayer(FloorLayerName);
		for (int i = -1; i < 2; i++)
		{
			for (int j = -1; j < 2; j++)
			{
				if (!floorMap[tilemapPosition.x + i, tilemapPosition.y + j])
				{
					count++;
				}
			}
		}

		return count;
	}

	internal void SetTreasure(Vector3Int treasurePosition)
	{
		var itemInstance = Instantiate(TreasurePrefab, this.transform);
		itemInstance.transform.position = CellToWorld(treasurePosition);
		itemInstance.Setup(treasurePosition);
		Interactables.Add(itemInstance);
	}

	internal void SetDroppedItem(Vector3Int treasurePosition, ItemDefinition item, int? stackStock = null)
	{
		var itemInstance = Instantiate(DroppedItemPrefab, this.transform);
		itemInstance.transform.position = CellToWorld(treasurePosition);
		itemInstance.Position = treasurePosition;
		itemInstance.InventoryItem = item.AsInventoryItem(stackStock);
		Interactables.Add(itemInstance);
	}

	internal void SetStairs(Vector3Int stairPosition)
	{
		var itemInstance = Instantiate(StairsPrefab, this.transform);
		itemInstance.transform.position = CellToWorld(stairPosition);
		itemInstance.Position = stairPosition;
		Interactables.Add(itemInstance);
	}

	internal void RemoveInteractable(Interactable currentInteractable)
	{
		if (currentInteractable == null) { return; }
		//sometimes don't destroy
		Interactables.Remove(currentInteractable);
		Destroy(currentInteractable.gameObject);
	}

	internal bool CanWalkTo(Vector3Int origin, Vector3Int destination)
	{
		var validWalkDirections = GetValidWalkDirections(origin);
		var validWalkPositions = validWalkDirections.Select(x => origin + GetFacingOffset(x));

		return validWalkPositions.Contains(destination);
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

	internal Vector3 CellToWorld(Vector3Int newMapPosition)
	{
		return newMapPosition * 2; //2 is tileworld.setting.cellsize
	}

	internal Interactable GetInteractable(Vector3Int tilemapPosition)
	{
		return Interactables.FirstOrDefault(x => x.Position == tilemapPosition);
	}

	internal bool IsWalkable(Vector3Int newMapPosition)
	{
		if (newMapPosition.x < 0 || newMapPosition.x >= dungeonWidth ||
		    newMapPosition.y < 0 || newMapPosition.y >= dungeonHeight) {
			return false;
		}

		bool[,] floorMap = _tileWorldCreator.GetMapOutputFromBlueprintLayer(FloorLayerName);
		return floorMap[newMapPosition.x, newMapPosition.y];
	}

	internal Vector3Int GetStartPositioon()
	{
		var floorMap = _tileWorldCreator.GetMapOutputFromBlueprintLayer(FloorLayerName);
		var startPos = Flatten(floorMap, (x) => x).Sample();

		return new Vector3Int(startPos.Coord.x, startPos.Coord.y, 0);
	}

	public static List<CoordValue<T>> Flatten<T>(T[,] arr, Func<T, bool> predicate = null)
	{
		int rows = arr.GetLength(0);
		int cols = arr.GetLength(1);

		List<CoordValue<T>> ret = new();
		for (int i = 0; i < rows; i++)
		{
			for (int j = 0; j < cols; j++)
			{
				var match = predicate == null || predicate(arr[i, j]);
				if (match)
				{
					ret.Add(new CoordValue<T>()
					{
						Coord = new Vector3Int(i, j),
						Value = arr[i, j]
					});
				}
			}
		}

		return ret;
	}

	//if not hallway it's a room
	public bool IsHallway(Vector3Int TilemapPosition)
	{
		//raycast in al directions
		var direction = new List<Facing>()
		{
			Facing.Up,
			Facing.Down,
			Facing.Left,
			Facing.Right,
			Facing.UpLeft,
			Facing.UpRight,
			Facing.DownLeft,
			Facing.DownRight
		};

		var walkableDirections = direction.Select(x =>
		{
			var target = GetRangedAttackPosition(null, TilemapPosition, x, 40, StopSight);
			var chebyshevDistance = Mathf.Max(Mathf.Abs(target.x - TilemapPosition.x), Mathf.Abs(target.y - TilemapPosition.y));
			var offset = TileWorldDungeon.GetFacingOffset(x);
			var walkable = CanWalkTo(TilemapPosition, TilemapPosition + offset);

			return new
			{
				direction = x,
				walkable = walkable
			};
		});

		var diagonalsWalkable = walkableDirections.Where(x =>
			x.direction == Facing.UpLeft ||
			x.direction == Facing.UpRight ||
			x.direction == Facing.DownLeft ||
			x.direction == Facing.DownRight)
			.Any(x => x.walkable);

		//var walkableCount = walkableDirections.Count(x => x.walkable);
		//var tiles = GetNeighborhoodTiles(TilemapPosition);
		//Debug.Log($"{tiles} blocked, {9 - tiles} walkable, count{walkableCount}, diag{diagonalsWalkable}");

		return !diagonalsWalkable;
	}

	public static bool StopSight(Vector3Int currentPosition, Vector3Int nextPosition, Character thrower)
	{
		bool hitWall = !Game.Instance.CurrentDungeon.IsWalkable(nextPosition);
		if (hitWall) { return true; }

		return false;
	}
}

public class CoordValue<T>
{
	public Vector3Int Coord;
	public T Value;
}
