using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using TWC;
using UnityEngine;

public class TileWorldDungeon : MonoBehaviour
{
	public List<Interactable> Interactables;

	public Gold GoldPrefab;
	public List<DroppedItem> DroppedItemPrefabs;
	public Stairs StairsPrefab;
	public List<Trap> TrapPrefabs;
	public GameObject SmallKeyPrefab;
	public GameObject LockedDoorPrefab;

	internal int dungeonWidth => Floor.Width;
	internal int dungeonHeight => Floor.Height;
	public bool IsThroneFloor;
	public bool IsExitFloor;
	// No boss floors exist yet; boss content sets this. Retreat is blocked while it is true.
	public bool IsBossFloor;

	private DungeonFloor runtimeFloor;
	public DungeonFloor Floor
    {
        get { EnsureRuntimeData(); return runtimeFloor; }
        private set => runtimeFloor = value;
    }

	[SerializeField] private TileWorldCreator _tileWorldCreator;
	private bool[,] floorMask;
	private bool[,] _isHallwayCache;

    internal bool EnsureRuntimeData()
    {
        if (runtimeFloor == null)
        {
            if (_tileWorldCreator == null || _tileWorldCreator.twcAsset == null) return false;
            // Core's immutable floor and multidimensional arrays aren't serialized by Unity.
            // Recreate only the deterministic data after a script reload, not scene contents.
            var generator = _tileWorldCreator.twcAsset.mapBlueprintLayers.SelectMany(layer => layer.stack)
                .Select(entry => entry.action).OfType<CoreDungeonLayerGenerator>().FirstOrDefault();
            if (generator == null) return false;
            runtimeFloor = CoreLayoutCache.GetDungeon(_tileWorldCreator, generator.Options(_tileWorldCreator));
        }
        if (floorMask == null) floorMask = runtimeFloor.Layers[DungeonLayers.Floor].ToArray();
        if (_isHallwayCache == null) InitializeCache();
        return true;
    }

	private void Awake()
	{
		Debug.Log("Dungeon created", this);
	}

    private void LateUpdate()
    {
        var game = Game.Instance;
        if (game == null || !game.IsReady || game.CurrentDungeon != this || game.TurnManager.IsProcessingTurn || !EnsureRuntimeData()) return;
        if (DungeonPlacement.Recover(this)) { game.RefreshSight(); game.UpdateMiniMap(); }
    }

	private void OnDestroy()
	{
		Debug.Log("Dungeon destroyed", this);
	}

	internal void Setup(TileWorldCreator twc, DungeonFloor floor)
	{
		this._tileWorldCreator = twc;
		this.Floor = floor;
		this.floorMask = floor.Layers[DungeonLayers.Floor].ToArray();
		this.IsThroneFloor = floor.IsThroneFloor;
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
		_isHallwayCache = new bool[runtimeFloor.Width, runtimeFloor.Height];
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
            tiles = GridSight.VisibleTiles(SightLayer, origin.ToGridPoint(), radius).ToCellSet();
            sightCache[key] = tiles;
        }
        return tiles;
    }

    // Closed doors are walls to sight: the door itself is visible, the vault behind it is not.
    private GridLayer sightLayer;
    private GridLayer SightLayer
    {
        get
        {
            if (sightLayer != null) return sightLayer;
            var mask = (bool[,])floorMask.Clone();
            foreach (var door in Interactables.OfType<DungeonProp>().Where(p => p.IsClosedDoor)) mask[door.Position.x, door.Position.y] = false;
            return sightLayer = new GridLayer(mask);
        }
    }

    internal void DoorsChanged() { sightLayer = null; sightCache.Clear(); }

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

	public Vector3Int GetStairPosition() => Floor.Stairs.ToCell();

	internal Vector3Int GetRandomOpenEnemyPosition()
	{
		bool[,] floorMap = floorMask;
		var flatMap = Flatten(floorMap, (x) => x);

		var allCharacterBounds = Game.Instance.AllCharacters.Select(x => x.ToBounds());

		var openPosition = flatMap
            .Where(x => IsWalkable(x.Coord))
			.Where(x => !Interactables.Any(y => y.Position == x.Coord))
			.Where(x => !allCharacterBounds.Any(y => y.Overlaps2D(x.Coord)))
			.Sample()
			.Coord;

		return openPosition;
	}

	internal List<Vector3Int> GetWalkableNeighborhoodTiles(Vector3Int tilemapPosition)
	{
		bool[,] floorMap = floorMask;
		List<Vector3Int> neighborhood = new();
		for (int i = -1; i < 2; i++)
		{
			for (int j = -1; j < 2; j++)
			{
				if (IsWalkable(tilemapPosition + new Vector3Int(i, j)))
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
		bool[,] floorMap = floorMask;
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

	internal void SetTreasure(Vector3Int treasurePosition, int? seededRoll = null)
	{
		var itemInstance = Instantiate(GoldPrefab, this.transform);
		itemInstance.transform.position = CellToWorld(treasurePosition);
		DungeonPresentation.GroundFloorObject(itemInstance.transform);
		itemInstance.Setup(treasurePosition);
        if(seededRoll.HasValue) itemInstance.SeededAmount = 5 + 4*(Mathf.Clamp(Game.Instance.PlayerController.Floor,1,30)-1) + (int)((uint)seededRoll.Value%4);
		Interactables.Add(itemInstance);
	}

    internal void SetGoldAmount(Vector3Int position, int amount)
    {
        var gold = Instantiate(GoldPrefab, transform);
        gold.transform.position = CellToWorld(position);
        DungeonPresentation.GroundFloorObject(gold.transform);
        gold.Setup(position); gold.SeededAmount = amount;
        Interactables.Add(gold);
    }

	internal DroppedItem SetDroppedItem(Vector3Int position, ItemDefinition item, int? stackStock = null)
	{
		var droppedItemPrefab = DroppedItemPrefabs.First(x => x.DroppedItemVisual == item.DroppedItemVisual);
		var itemInstance = Instantiate(droppedItemPrefab, this.transform);
		itemInstance.transform.position = CellToWorld(position);
		DungeonPresentation.GroundFloorObject(itemInstance.transform);
		itemInstance.Position = position;
		itemInstance.InventoryItem = item.AsInventoryItem(stackStock);
		Interactables.Add(itemInstance);
		return itemInstance;
	}

	internal void SetTrap(Vector3Int position, Trap trap = null)
	{
		var trapPrefab = trap != null ? trap : FantasyTrap.PrefabFor(UnityEngine.Random.Range(0, FantasyTrap.KindCount));
		var trapInstance = Instantiate(trapPrefab, this.transform);
		trapInstance.transform.position = CellToWorld(position);
		DungeonPresentation.GroundFloorObject(trapInstance.transform);
		trapInstance.Position = position;
		trapInstance.VisualObject.gameObject.SetActive(false);
		Interactables.Add(trapInstance);
	}

	internal void SetTrap(Vector3Int position, int roll)
	{
		var trapPrefab = FantasyTrap.PrefabFor(roll);
		var trapInstance = Instantiate(trapPrefab, this.transform);
		trapInstance.transform.position = CellToWorld(position);
		DungeonPresentation.GroundFloorObject(trapInstance.transform);
		trapInstance.Position = position;
		trapInstance.VisualObject.gameObject.SetActive(false);
		Interactables.Add(trapInstance);
	}

	internal void SetSmallKey(Vector3Int position)
	{
		var key = SmallKey.Create(this, position);
		Interactables.Add(key);
	}

	internal void SetStairs(Vector3Int stairPosition)
	{
		var itemInstance = Instantiate(StairsPrefab, this.transform);
		itemInstance.transform.position = CellToWorld(stairPosition);
		DungeonPresentation.GroundFloorObject(itemInstance.transform);
		itemInstance.Position = stairPosition;
		Interactables.Add(itemInstance);
	}

	internal void RemoveInteractable(Interactable currentInteractable)
	{
		if (currentInteractable == null) { return; }
		//sometimes don't destroy
		Interactables.Remove(currentInteractable);
		if (Application.isPlaying) Destroy(currentInteractable.gameObject);
		else DestroyImmediate(currentInteractable.gameObject);
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

	internal bool[,] GetFloorMask() => (bool[,])floorMask.Clone();

	internal Vector3Int? GetStairsCell()
	{
		var stairs = Interactables.OfType<Stairs>().FirstOrDefault();
		return stairs == null ? null : stairs.Position;
	}

	// Makes hidden traps visible (same as having triggered them). Returns how many were newly revealed.
	internal int RevealTrapsAround(Vector3Int center, int radius)
	{
		int revealed = 0;
		foreach (var trap in Interactables.OfType<Trap>())
		{
			if (trap == null || trap.VisualObject == null || trap.VisualObject.activeSelf) continue;
			if (ChevDistance(trap.Position, center) > radius) continue;
			trap.VisualObject.SetActive(true);
			revealed++;
		}
		return revealed;
	}

	// Revealed (visible) non-party trap on one of the 8 neighbours or the centre; the facing tile wins.
	internal Trap FindAdjacentRevealedTrap(Vector3Int center, Facing preferred)
	{
		foreach (var cell in AdjacentCells(center, preferred).Prepend(center))
		{
			var trap = Interactables.OfType<Trap>().FirstOrDefault(t => t != null && t.Position == cell &&
				t is not CaltropTrap && t.VisualObject != null && t.VisualObject.activeSelf);
			if (trap != null) return trap;
		}
		return null;
	}

	// Walkable neighbour with no interactable and no character; the facing tile wins.
	internal Vector3Int? FindFreeAdjacentTile(Vector3Int center, Facing preferred)
	{
		foreach (var cell in AdjacentCells(center, preferred))
		{
			if (!IsWalkable(cell) || GetInteractable(cell) != null) continue;
			if (Game.Instance.AllCharacters.Any(c => c != null && Character.Contains2D(c.ToBounds(), cell))) continue;
			return cell;
		}
		return null;
	}

	private IEnumerable<Vector3Int> AdjacentCells(Vector3Int center, Facing preferred)
	{
		var first = center + GetFacingOffset(preferred);
		yield return first;
		for (int dx = -1; dx <= 1; dx++)
			for (int dy = -1; dy <= 1; dy++)
			{
				var cell = new Vector3Int(center.x + dx, center.y + dy, center.z);
				if ((dx != 0 || dy != 0) && cell != first) yield return cell;
			}
	}

    internal bool IsFloorCell(Vector3Int cell) => Floor != null && GridMovement.IsWalkable(floorMask, cell);
    internal DungeonProp PropAt(Vector3Int cell) => Interactables.OfType<DungeonProp>().FirstOrDefault(p => p.Position == cell && !p.Opened);
    internal bool IsHazard(Vector3Int cell) => PropAt(cell)?.Definition.Kind == DungeonSceneryKind.Hazard;
    internal IDungeonDamageTarget DamageTargetAt(Vector3Int cell)
    {
        var character = GetCharacterAtPosition(cell);
        if(character != null) return new CharacterDamageTarget(character);
        var prop = PropAt(cell);
        return prop != null && prop.Alive ? prop : null;
    }
    internal List<GameAction> EntryEffects(Character character, Vector3Int from, Vector3Int to)
    {
        if (from == to || !IsHazard(to)) return new();
        return new() { new TakeDamageAction(character,character,Mathf.CeilToInt(character.FinalStats.HPMax*.1f)) { Environmental = true } };
    }
	internal bool IsWalkable(Vector3Int newMapPosition)
	{
		return IsFloorCell(newMapPosition) && !Interactables.OfType<DungeonProp>().Any(p => p.Position == newMapPosition && p.BlocksMovement);
	}

	internal Vector3Int GetStartPosition() => Floor.Start.ToCell();

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
        !Floor.IsRoom(position.ToGridPoint());

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
		bool hitWall = !Game.Instance.CurrentDungeon.IsFloorCell(nextPosition);
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
