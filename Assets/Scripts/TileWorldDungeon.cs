using System;
using System.Collections.Generic;
using System.Linq;
using TWC;
using UnityEngine;

public class TileWorldDungeon : MonoBehaviour
{
	public string FloorLayerName;
	public string StairPositionLayerName;

	public List<Interactable> Interactables;

	public Gold GoldPrefab;
	public List<DroppedItem> DroppedItemPrefabs;
	public Stairs StairsPrefab;
	public List<Trap> TrapPrefabs;

	internal int dungeonWidth => _tileWorldCreator.twcAsset.mapWidth;
	internal int dungeonHeight => _tileWorldCreator.twcAsset.mapHeight;
	public bool IsThroneFloor;
	public bool IsExitFloor;

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

	//This should be getcharacteratposition
	internal Character GetCharacterAtPosition(Vector3Int attackPosition)
	{
		return Game.Instance.AllCharacters
			.Select(x => new
			{
				character = x,
				bounds = x.ToBounds()
			})
			.FirstOrDefault(x => x.bounds.Contains(attackPosition))
			?.character;
	}

	internal void InitializeCache()
	{
		sightCache.Clear();
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
		return IsWalkable(newMapPosition);
	}

	internal List<Facing> GetValidWalkDirections(Vector3Int tilemapPosition)
	{
		return GridMovement.GetValidDirections(tilemapPosition, CanWalk).ToList();
	}

    private readonly Dictionary<(Vector3Int, int), HashSet<Vector3Int>> sightCache = new();

    internal HashSet<Vector3Int> GetVisibleTiles(Character character, Vector3Int origin)
    {
        if (!GridMovement.Contains(dungeonWidth, dungeonHeight, origin)) return new();
        int radius = _isHallwayCache[origin.x, origin.y]
            ? (character.FootPrint == FootPrint.Size3x3 ? 2 : 1) : 8;
        var key = (origin, radius);
        if (!sightCache.TryGetValue(key, out var tiles))
        {
            tiles = DungeonSight.VisibleTiles(_tileWorldCreator.GetMapOutputFromBlueprintLayer(FloorLayerName), origin, radius);
            sightCache[key] = tiles;
        }
        return tiles;
    }

    internal bool CanSee(Character observer, Character target)
    {
        var tiles = GetVisibleTiles(observer, observer.TilemapPosition);
        foreach (var cell in target.ToBounds().allPositionsWithin)
            if (tiles.Contains(cell)) return true;
        return false;
    }

    internal Vector3Int WorldToCell(Vector3 position)
    {
        float size = _tileWorldCreator.twcAsset.cellSize;
        return new Vector3Int(Mathf.RoundToInt(position.x / size), Mathf.RoundToInt(position.y / size), 0);
    }

	static public Vector3Int GetFacingOffset(Facing facing)
	{
		return GridMovement.GetFacingOffset(facing);
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

		if (!GridMovement.Contains(dungeonWidth, dungeonHeight, startPosition)) return startPosition;
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

		// Preserve the existing origin fallback when no suitable cell is reachable.
		if (path.Count == 0) return startPosition;
		BFS.Node node = path.Last();
		return new Vector3Int(node.X, node.Y);
	}

	public Vector3Int GetPositionWith(Vector3Int startPosition, Func<BFS.Node, bool> isTargetNode)
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

		if (!GridMovement.Contains(dungeonWidth, dungeonHeight, startPosition)) return startPosition;
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
				return isTargetNode(node);
			});

		// Preserve the existing origin fallback when no suitable cell is reachable.
		if (path.Count == 0) return startPosition;
		BFS.Node node = path.Last();
		return new Vector3Int(node.X, node.Y);
	}

	public Vector3Int GetStairPosition(bool isThroneFloor)
	{
		if (!isThroneFloor) { return GetRandomOpenEnemyPosition(); }

		//var floorMap = _tileWorldCreator.GetMapOutputFromBlueprintLayer(StairPositionLayerName);
		//var startPos = Flatten(floorMap, (x) => x).Sample();

		//return new Vector3Int(startPos.Coord.x, startPos.Coord.y, 0);
		return new Vector3Int(6, 9);
	}

	internal Vector3Int GetRandomOpenEnemyPosition()
	{
		bool[,] floorMap = _tileWorldCreator.GetMapOutputFromBlueprintLayer(FloorLayerName);
		var flatMap = Flatten(floorMap, (x) => x);

		var allCharacterBounds = Game.Instance.AllCharacters.Select(x => x.ToBounds());

		var openPosition = flatMap
			.Where(x => !Interactables.Any(y => y.Position == x.Coord))
			.Where(x => !allCharacterBounds.Any(y => y.Overlaps2D(x.Coord)))
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
				if (GridMovement.IsWalkable(floorMap, tilemapPosition + new Vector3Int(i, j)))
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
				if (!GridMovement.IsWalkable(floorMap, tilemapPosition + new Vector3Int(i, j)))
				{
					count++;
				}
			}
		}

		return count;
	}

	internal void SetTreasure(Vector3Int treasurePosition)
	{
		var itemInstance = Instantiate(GoldPrefab, this.transform);
		itemInstance.transform.position = CellToWorld(treasurePosition);
		itemInstance.Setup(treasurePosition);
		Interactables.Add(itemInstance);
	}

	internal DroppedItem SetDroppedItem(Vector3Int position, ItemDefinition item, int? stackStock = null)
	{
		var droppedItemPrefab = DroppedItemPrefabs.First(x => x.DroppedItemVisual == item.DroppedItemVisual);
		var itemInstance = Instantiate(droppedItemPrefab, this.transform);
		itemInstance.transform.position = CellToWorld(position);
		itemInstance.Position = position;
		itemInstance.InventoryItem = item.AsInventoryItem(stackStock);
		Interactables.Add(itemInstance);
		return itemInstance;
	}

	internal void SetTrap(Vector3Int position, Trap trap = null)
	{
		var trapPrefab = TrapPrefabs.Sample();
		var trapInstance = Instantiate(trapPrefab, this.transform);
		trapInstance.transform.position = CellToWorld(position);
		trapInstance.Position = position;
		trapInstance.VisualObject.gameObject.SetActive(false);
		Interactables.Add(trapInstance);
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
		return GridMovement.CanStep(origin, destination, CanWalk);
	}

	internal Vector3Int GetRangedAttackPosition(
		Character thrower,
		Vector3Int origin,
		Facing direction,
		int maxRange,
		Func<Vector3Int, Vector3Int, Character, bool, bool> stopCondition)
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

			if (stopCondition.Invoke(currentPosition, nextPosition, thrower, false))
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
		return GridMovement.CellToWorld(newMapPosition, _tileWorldCreator.twcAsset.cellSize);
	}

	internal Interactable GetInteractable(Vector3Int tilemapPosition)
	{
		return Interactables.FirstOrDefault(x => x.Position == tilemapPosition);
	}

	internal bool IsWalkable(Vector3Int newMapPosition)
	{
		return _tileWorldCreator != null && GridMovement.IsWalkable(
			_tileWorldCreator.GetMapOutputFromBlueprintLayer(FloorLayerName), newMapPosition);
	}

	internal Vector3Int GetStartPosition(bool throneFloor)
	{
		if (!throneFloor)
		{
			var floorMap = _tileWorldCreator.GetMapOutputFromBlueprintLayer(FloorLayerName);
			var startPos = Flatten(floorMap, (x) => x).Sample();

			return new Vector3Int(startPos.Coord.x, startPos.Coord.y, 0);
		}

		return new Vector3Int(6, 4);
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
    public bool IsHallway(Vector3Int position) =>
        !DungeonSight.IsRoom(_tileWorldCreator.GetMapOutputFromBlueprintLayer(FloorLayerName), position);

	public static int ChevDistance(Vector3Int a, Vector3Int b)
	{
		return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
	}

	public static int ManhattanDistance(Vector3Int a, Vector3Int b)
	{
		return Mathf.Abs(a.x - b.x)
			 + Mathf.Abs(a.y - b.y);
	}

	public static bool StopSight(Vector3Int currentPosition, Vector3Int nextPosition, Character thrower, bool hitFriendly)
	{
		bool hitWall = !Game.Instance.CurrentDungeon.IsWalkable(nextPosition);
		if (hitWall) { return true; }

		return false;
	}

	internal Character OverlapsAnyOtherCharacter(Character character, bool excludeAllies = false)
	{
		return OverlapsAnyOtherCharacter(character, character.ToBounds(), excludeAllies);
	}

	internal Character OverlapsAnyOtherCharacter(Character character, BoundsInt boundsInt, bool excludeAllies = false)
	{
		var others = Game.Instance.AllCharacters.AsEnumerable()
			.Where(x => x != character);

		if (excludeAllies)
		{
			others = others
				.Where(x => x.Team != character.Team);
		}

		return others
			.FirstOrDefault(x => x.OverlapsWith(boundsInt));
	}
}

public class CoordValue<T>
{
	public Vector3Int Coord;
	public T Value;
}
