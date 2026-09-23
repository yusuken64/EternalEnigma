using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Validation;

public sealed class TownPlanValidationResult
{
    public IReadOnlyList<string> Errors { get; }
    public bool IsValid => Errors.Count == 0;
    internal TownPlanValidationResult(IEnumerable<string> errors) { Errors = Array.AsReadOnly(errors.ToArray()); }
}

/// <summary>
/// Validates structural, slot, and reachability properties of a TownPlan.
/// The generator retries on failure and tests assert validity across seeds.
/// </summary>
public static class TownPlanValidator
{
    public static TownPlanValidationResult Validate(TownPlan? plan, TownPlanOptions? options)
    {
        var errors = new List<string>();

        // Null check
        if (plan == null)
        {
            errors.Add("plan: Argument null.");
            return new TownPlanValidationResult(errors);
        }
        if (options == null)
        {
            errors.Add("options: Argument null.");
            return new TownPlanValidationResult(errors);
        }

        void Check(bool condition, string error) { if (!condition) errors.Add(error); }

        // Check 1: Dims equal options; plan.Seed == options.Seed; PartySpawn/Exit equal options
        Check(plan.Width == options.Width && plan.Height == options.Height,
            "dims: Plan dimensions do not match options.");
        Check(plan.Seed == options.Seed, "seed: Plan seed does not match options.");
        Check(plan.PartySpawn.Equals(options.PartySpawn), "partySpawn: Plan PartySpawn does not match options.");
        Check(plan.Exit.Equals(options.Exit), "exit: Plan Exit does not match options.");

        // Check 2: Every layer in TownLayers.All present with equal dims
        foreach (var layerName in TownLayers.All)
        {
            Check(plan.Layers.ContainsKey(layerName), $"layers: Missing layer '{layerName}'.");
        }

        if (errors.Count > 0) return new TownPlanValidationResult(errors);

        var layers = plan.Layers;
        var walkableLayer = layers[TownLayers.Walkable];
        var housesLayer = layers[TownLayers.Houses];
        var treesLayer = layers[TownLayers.Trees];
        var shopsWallsLayer = layers[TownLayers.ShopWalls];
        var buildingsLayer = layers[TownLayers.Buildings];
        var alliesLayer = layers[TownLayers.Allies];
        var dungeonLayer = layers[TownLayers.Dungeon];
        var roofLayer = layers[TownLayers.Roofs];
        var shopFloorLayer = layers[TownLayers.ShopFloor];

        // Check 3: Walkable == !(Houses | Trees | ShopWalls) everywhere
        for (int x = 0; x < plan.Width; x++)
        {
            for (int y = 0; y < plan.Height; y++)
            {
                bool walkable = walkableLayer[x, y];
                bool blocked = housesLayer[x, y] || treesLayer[x, y] || shopsWallsLayer[x, y];
                bool expectedWalkable = !blocked;

                if (walkable != expectedWalkable)
                {
                    Check(false, $"walkable: Walkable layer does not equal !(Houses | Trees | ShopWalls) at ({x},{y}).");
                    break;
                }
            }
            if (errors.Count > 0 && errors.Any(e => e.StartsWith("walkable:"))) break;
        }

        // Check 4: Every cell with IsReservedCorridor is Walkable and false in Houses, Trees, Buildings, Allies, ShopWalls
        for (int x = 0; x < plan.Width; x++)
        {
            for (int y = 0; y < plan.Height; y++)
            {
                var cell = new GridPoint(x, y);
                if (TownPlan.IsReservedCorridor(cell, plan.Height))
                {
                    Check(walkableLayer[x, y], $"corridor.walkable: Reserved corridor at ({x},{y}) is not walkable.");
                    Check(!housesLayer[x, y], $"corridor.houses: Reserved corridor at ({x},{y}) contains a house.");
                    Check(!treesLayer[x, y], $"corridor.trees: Reserved corridor at ({x},{y}) contains a tree.");
                    Check(!buildingsLayer[x, y], $"corridor.buildings: Reserved corridor at ({x},{y}) contains a building.");
                    Check(!alliesLayer[x, y], $"corridor.allies: Reserved corridor at ({x},{y}) contains an ally.");
                    Check(!shopsWallsLayer[x, y], $"corridor.shopWalls: Reserved corridor at ({x},{y}) contains a shop wall.");
                }
            }
        }

        // Check 5: BuildingSlots validation
        Check(plan.BuildingSlots.Count == options.BuildingCount,
            $"buildingSlots.count: Expected {options.BuildingCount} building slots, got {plan.BuildingSlots.Count}.");

        // Verify strictly increasing in raster order
        for (int i = 0; i < plan.BuildingSlots.Count; i++)
        {
            var slot = plan.BuildingSlots[i];

            Check(walkableLayer[slot.X, slot.Y], $"buildingSlots[{i}]: Slot at {slot} is not walkable.");

            // Check not on border
            Check(slot.X > 0 && slot.X < plan.Width - 1 && slot.Y > 0 && slot.Y < plan.Height - 1,
                $"buildingSlots[{i}]: Slot at {slot} is on the border.");

            // Check strictly increasing in raster order
            if (i > 0)
            {
                var prev = plan.BuildingSlots[i - 1];
                bool prevLess = prev.Y < slot.Y || (prev.Y == slot.Y && prev.X < slot.X);
                Check(prevLess, $"buildingSlots: Not in strictly increasing raster order at index {i}.");
            }

            // Check Chebyshev distance >= 3 from other building slots
            for (int j = 0; j < plan.BuildingSlots.Count; j++)
            {
                if (i != j)
                {
                    var other = plan.BuildingSlots[j];
                    int chebyshev = Math.Max(Math.Abs(slot.X - other.X), Math.Abs(slot.Y - other.Y));
                    Check(chebyshev >= 3, $"buildingSlots: Distance between slots {i} and {j} is less than 3.");
                }
            }
        }

        // Check 6: AllySlots validation
        Check(plan.AllySlots.Count == options.AllyCount,
            $"allySlots.count: Expected {options.AllyCount} ally slots, got {plan.AllySlots.Count}.");

        var allyLocations = new HashSet<GridPoint>();
        var buildingSlotSet = new HashSet<GridPoint>(plan.BuildingSlots);
        var shopFloorCells = new HashSet<GridPoint>();
        var shopWallsCells = new HashSet<GridPoint>();

        // Collect all shop room cells
        foreach (var room in plan.ShopRooms)
        {
            foreach (var cell in room.Floor)
            {
                shopFloorCells.Add(cell);
            }
            foreach (var cell in room.Wall)
            {
                shopWallsCells.Add(cell);
            }
        }

        var allShopCells = new HashSet<GridPoint>(shopFloorCells);
        foreach (var cell in shopWallsCells)
        {
            allShopCells.Add(cell);
        }

        for (int i = 0; i < plan.AllySlots.Count; i++)
        {
            var slot = plan.AllySlots[i];
            var cell = slot.Cell;

            Check(walkableLayer[cell.X, cell.Y], $"allySlots[{i}]: Slot at {cell} is not walkable.");

            Check(!buildingSlotSet.Contains(cell), $"allySlots[{i}]: Slot at {cell} overlaps with a building slot.");
            Check(!allShopCells.Contains(cell), $"allySlots[{i}]: Slot at {cell} overlaps with a shop room.");
            Check(!cell.Equals(plan.PartySpawn), $"allySlots[{i}]: Slot at {cell} overlaps with PartySpawn.");
            Check(!cell.Equals(plan.Exit), $"allySlots[{i}]: Slot at {cell} overlaps with Exit.");
            Check(!cell.Equals(plan.DungeonEntrance), $"allySlots[{i}]: Slot at {cell} overlaps with DungeonEntrance.");

            allyLocations.Add(cell);
        }

        // Check 7: ShopFlags and ShopRooms validation
        int shopCount = options.ShopFlags.Count(f => f);
        Check(plan.ShopRooms.Count == shopCount,
            $"shopRooms.count: Expected {shopCount} shop rooms, got {plan.ShopRooms.Count}.");

        // Verify ShopFlags correspondence with ShopRooms doors at BuildingSlots
        for (int i = 0; i < options.BuildingCount; i++)
        {
            bool isShop = i < options.ShopFlags.Count && options.ShopFlags[i];
            var buildingSlot = plan.BuildingSlots[i];
            var room = plan.ShopRoomAt(buildingSlot);

            if (isShop)
            {
                Check(room != null, $"shops[{i}]: ShopFlags[{i}] is true but no room exists at building slot {i}.");
            }
            else
            {
                Check(room == null, $"shops[{i}]: ShopFlags[{i}] is false but a room exists at building slot {i}.");
            }
        }

        // Validate each shop room
        for (int i = 0; i < plan.ShopRooms.Count; i++)
        {
            var room = plan.ShopRooms[i];

            // Door must be in BuildingSlots (implicit in the correspondence check above)
            Check(plan.BuildingIndexAt(room.Door) != null, $"shopRoom[{i}]: Door at {room.Door} is not a building slot.");

            // Every Floor cell must be true in ShopFloor and Walkable
            foreach (var cell in room.Floor)
            {
                Check(shopFloorLayer[cell.X, cell.Y], $"shopRoom[{i}]: Floor cell {cell} is not true in ShopFloor layer.");
                Check(walkableLayer[cell.X, cell.Y], $"shopRoom[{i}]: Floor cell {cell} is not walkable.");
            }

            // Every Wall cell must be true in ShopWalls
            foreach (var cell in room.Wall)
            {
                Check(shopsWallsLayer[cell.X, cell.Y], $"shopRoom[{i}]: Wall cell {cell} is not true in ShopWalls layer.");
            }

            // VendorAnchor must be true in ShopFloor
            var anchor = room.VendorAnchor;
            Check(shopFloorLayer[anchor.X, anchor.Y], $"shopRoom[{i}]: VendorAnchor {anchor} is not true in ShopFloor layer.");
        }

        // ShopFloor and ShopWalls must contain no cells outside the rooms
        for (int x = 0; x < plan.Width; x++)
        {
            for (int y = 0; y < plan.Height; y++)
            {
                var cell = new GridPoint(x, y);
                if (shopFloorLayer[x, y])
                {
                    Check(shopFloorCells.Contains(cell), $"shopFloor: Cell {cell} is in ShopFloor layer but not in any room.");
                }
                if (shopsWallsLayer[x, y])
                {
                    Check(shopWallsCells.Contains(cell), $"shopWalls: Cell {cell} is in ShopWalls layer but not in any room.");
                }
            }
        }

        // Check 8: Dungeon layer has exactly one true cell and it equals DungeonEntrance
        int dungeonCellCount = 0;
        GridPoint? dungeonCell = null;
        for (int x = 0; x < plan.Width; x++)
        {
            for (int y = 0; y < plan.Height; y++)
            {
                if (dungeonLayer[x, y])
                {
                    dungeonCellCount++;
                    dungeonCell = new GridPoint(x, y);
                }
            }
        }

        Check(dungeonCellCount == 1, $"dungeon: Expected exactly 1 dungeon cell, found {dungeonCellCount}.");
        if (dungeonCell.HasValue)
        {
            Check(dungeonCell.Equals(plan.DungeonEntrance),
                $"dungeon: Dungeon cell {dungeonCell} does not equal DungeonEntrance {plan.DungeonEntrance}.");
        }

        // Check 9: Roofs == Houses everywhere
        for (int x = 0; x < plan.Width; x++)
        {
            for (int y = 0; y < plan.Height; y++)
            {
                bool roofValue = roofLayer[x, y];
                bool houseValue = housesLayer[x, y];

                if (roofValue != houseValue)
                {
                    Check(false, $"roofs: Roofs layer does not equal Houses layer at ({x},{y}).");
                    break;
                }
            }
            if (errors.Count > 0 && errors.Any(e => e.StartsWith("roofs:"))) break;
        }

        // Check 10: Reachability check
        var reachable = GridSearch.VisitOrder(plan.PartySpawn, plan.CardinalNeighbors);
        var reachableSet = new HashSet<GridPoint>(reachable);

        var unreachableTargets = new List<string>();

        // Check each building slot is reachable
        foreach (var slot in plan.BuildingSlots)
        {
            if (!reachableSet.Contains(slot))
            {
                unreachableTargets.Add($"building slot {slot}");
            }
        }

        // Check each ally slot is reachable
        foreach (var slot in plan.AllySlots)
        {
            if (!reachableSet.Contains(slot.Cell))
            {
                unreachableTargets.Add($"ally slot {slot.Cell}");
            }
        }

        // Check each shop room floor cell is reachable
        foreach (var room in plan.ShopRooms)
        {
            foreach (var cell in room.Floor)
            {
                if (!reachableSet.Contains(cell))
                {
                    unreachableTargets.Add($"shop floor {cell}");
                    break; // Only report once per room
                }
            }
        }

        // Check Exit is reachable
        if (!reachableSet.Contains(plan.Exit))
        {
            unreachableTargets.Add($"exit {plan.Exit}");
        }

        // Check DungeonEntrance is reachable
        if (!reachableSet.Contains(plan.DungeonEntrance))
        {
            unreachableTargets.Add($"dungeon entrance {plan.DungeonEntrance}");
        }

        if (unreachableTargets.Count > 0)
        {
            Check(false, $"reachability: Unreachable targets: {string.Join(", ", unreachableTargets)}.");
        }

        return new TownPlanValidationResult(errors);
    }
}
