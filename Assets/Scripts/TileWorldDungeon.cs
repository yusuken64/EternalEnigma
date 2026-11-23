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
	// True while the floor's guardian is alive. Retreat becomes available after its defeat.
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
    private float cellSize;
    private GameObject presentationWorld;
    internal float PresentationWallHeight => runtimeFloor?.Variant == DungeonVariant.Town
        ? presentationWorld.GetComponentsInChildren<Renderer>(true).Select(r => -r.bounds.min.z).DefaultIfEmpty(0).Max()
        : _tileWorldCreator?.twcAsset.mapBuildLayers.OfType<DungeonBoundaryLayer>()
        .Where(l=>l.active&&l.SmartPreset!=null).Select(l=>l.WallHeight).DefaultIfEmpty(0).Max() ?? 0;

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
        cellSize = twc.twcAsset.cellSize;
        presentationWorld = twc.worldObject;
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

	internal List<Facing> GetValidWalkDirections(Vector3Int tilemapPosition, FootPrint footprint = FootPrint.Size1x1)
	{
		return GridMovement.GetValidDirections(tilemapPosition, CanWalk, DiagonalMovement.AllowCornerCutting)
            .Where(f => CanWalkTo(tilemapPosition, tilemapPosition + GridMovement.GetFacingOffset(f), footprint)).ToList();
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
            if (runtimeFloor.Variant == DungeonVariant.Overworld && sightCache.Count >= 512) sightCache.Clear();
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
            if (runtimeFloor.Variant == DungeonVariant.Overworld && Game.Instance?.Finale != null)
                for (int y = 0; y < runtimeFloor.Height; y++) for (int x = 0; x < runtimeFloor.Width; x++)
                    mask[x, y] = Game.Instance.Finale.Session.IsWalkable(new EternalEnigma.Core.World.GridPoint(x,y));
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
        AutoplayRunner.ObserveLoot(itemInstance);
	}

    internal void SetGoldAmount(Vector3Int position, int amount)
    {
        var gold = Instantiate(GoldPrefab, transform);
        gold.transform.position = CellToWorld(position);
        DungeonPresentation.GroundFloorObject(gold.transform);
        gold.Setup(position); gold.SeededAmount = amount;
        Interactables.Add(gold);
        AutoplayRunner.ObserveLoot(gold);
    }

	internal DroppedItem SetDroppedItem(Vector3Int position, ItemDefinition item, int? stackStock = null)
	{
		var droppedItemPrefab = item.ResolveDroppedPrefab(DroppedItemPrefabs);
		var itemInstance = Instantiate(droppedItemPrefab, this.transform);
		itemInstance.transform.position = CellToWorld(position);
		DungeonPresentation.GroundFloorObject(itemInstance.transform);
		itemInstance.Position = position;
		itemInstance.InventoryItem = item.AsInventoryItem(stackStock);
		Interactables.Add(itemInstance);
        AutoplayRunner.ObserveLoot(itemInstance);
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
        AutoplayRunner.ObserveLoot(key);
	}

	internal void SetStairs(Vector3Int stairPosition)
	{
		var itemInstance = Instantiate(StairsPrefab, this.transform);
		itemInstance.Place(this, stairPosition);
		if (Floor.Variant == DungeonVariant.Town)
            foreach (var renderer in itemInstance.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
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

    internal bool CanWalkTo(Vector3Int origin, Vector3Int destination) => CanWalkTo(origin, destination, FootPrint.Size1x1);

    // Availability is per tile, so occupancy and exploration constraints also cover large actors.
    internal bool CanWalkTo(Vector3Int origin, Vector3Int destination, FootPrint footprint,
        Func<Vector3Int, bool> available = null)
    {
        if (!AllowsDoorwayStep(origin, destination)) return false;
        bool Walkable(Vector3Int cell) => IsWalkable(cell) && (available == null || available(cell));
        var delta = destination - origin;
        foreach (var cell in Character.ToBounds(footprint, origin).allPositionsWithin)
        {
            if (Floor.Variant == DungeonVariant.Town && !AllowsDoorwayStep(cell, cell + delta)) return false;
            if (!GridMovement.CanStep(cell, cell + delta, Walkable, canPassBlockedSide: IsOutdoorTreeCorner)) return false;
        }
        return true;
    }

    internal bool IsOutdoorTreeCorner(Vector3Int cell)
    {
        var floor = Floor;
        var town = floor?.TownPlan;
        if (floor?.Variant != DungeonVariant.Town || town == null ||
            !GridMovement.Contains(town.Width, town.Height, cell) ||
            cell.x == 0 || cell.y == 0 || cell.x == town.Width - 1 || cell.y == town.Height - 1) return false;
        var point = cell.ToGridPoint();
        bool On(string name) => town.Layers.TryGetValue(name, out var layer) && layer.At(point);
        // Tree overlap never turns a building, interior, doorway or enclosing wall into a soft corner.
        return On(TownLayers.Trees) && !On(TownLayers.Houses) && !On(TownLayers.ShopWalls) &&
            !On(TownLayers.ShopFloor) && !On(TownLayers.Roofs) && !On(TownLayers.Furniture) &&
            !On(TownLayers.Counters) && !On(TownLayers.Carpet) && !On(TownLayers.Boundary) &&
            !town.IsDoorway(point) &&
            !Interactables.OfType<DungeonProp>().Any(p => p.Position == cell && p.BlocksMovement) &&
            (Game.Instance?.CurrentDungeon != this || Game.Instance.Encounter?.Blocks(cell) != true);
    }

    // Combat and adjacent interactions retain the original strict corner rule.
    internal bool CanReachAdjacent(Vector3Int origin, Vector3Int destination) =>
        AllowsDoorwayStep(origin, destination) && GridMovement.CanStep(origin, destination, CanWalk);

    internal bool AllowsDoorwayStep(Vector3Int from, Vector3Int to) =>
        Floor.Variant == DungeonVariant.Overworld ? Game.Instance.Finale.Session.CanStep(from.ToGridPoint(), to.ToGridPoint()) :
        Floor.TownPlan?.AllowsDoorwayStep(from.ToGridPoint(), to.ToGridPoint()) ?? true;

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

    internal float CellSize => cellSize > 0 ? cellSize : _tileWorldCreator.twcAsset.cellSize;

    // Movement roots use the cell origin; models and ground markers use its center.
	internal Vector3 CellToWorld(Vector3Int newMapPosition) => GridMovement.CellToWorld(newMapPosition, CellSize);

    internal Vector3Int GroundPointToCell(Vector3 point) =>
        new(Mathf.FloorToInt(point.x / CellSize), Mathf.FloorToInt(point.y / CellSize), 0);

    internal Vector3 CellCenterToWorld(Vector3Int cell) =>
        CellToWorld(cell) + new Vector3(CellSize * .5f, CellSize * .5f, 0);

	internal Interactable GetInteractable(Vector3Int tilemapPosition)
	{
		// Trail pickups must not hide the stairs, traps, or other interactions beneath them.
		return Interactables.FirstOrDefault(x => x != null && x.Position == tilemapPosition && x is not TrailCoin)
			?? Interactables.FirstOrDefault(x => x != null && x.Position == tilemapPosition);
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
    internal DungeonProp PropAt(Vector3Int cell)
    {
        foreach (var interactable in Interactables)
            if (interactable is DungeonProp prop && prop.Position == cell && !prop.Opened) return prop;
        return null;
    }
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

        if (runtimeFloor?.Variant == DungeonVariant.Overworld && Game.Instance?.Finale != null)
            return Game.Instance.Finale.Session.IsWalkable(newMapPosition.ToGridPoint());
		return IsFloorCell(newMapPosition) && !Interactables.OfType<DungeonProp>().Any(p => p.Position == newMapPosition && p.BlocksMovement) &&
            (Game.Instance?.CurrentDungeon != this || Game.Instance.Encounter?.Blocks(newMapPosition) != true);
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
        => FirstOccupant(character, boundsInt, excludeAllies, false);

    internal Character LivingOccupant(Vector3Int cell, Character except)
        => FirstOccupant(except, new BoundsInt(cell, Vector3Int.one), false, true);

    private static Character FirstOccupant(Character except, BoundsInt bounds, bool excludeAllies, bool livingOnly)
    {
        var game = Game.Instance;
        // Preserve the same party-first order when more than one actor overlaps the cell.
        bool Matches(Character candidate) => candidate != null && candidate != except &&
            (!livingOnly || candidate.Vitals.HP > 0) && (!excludeAllies || candidate.Team != except.Team) &&
            candidate.OverlapsWith(bounds);
        foreach (var ally in game.Allies) if (Matches(ally)) return ally;
        foreach (var enemy in game.Enemies) if (Matches(enemy)) return enemy;
        return null;
    }
}

public class CoordValue<T>
{
	public Vector3Int Coord;
	public T Value;
}
