using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Validation;

public sealed class DungeonFloorValidationResult
{
    public IReadOnlyList<string> Errors { get; }
    public bool IsValid => Errors.Count == 0;
    internal DungeonFloorValidationResult(IEnumerable<string> errors) { Errors = Array.AsReadOnly(errors.ToArray()); }
}

/// <summary>
/// Validates structural and reachability properties of a generated dungeon floor.
/// Checks dimensions, seed, boundaries, layer relationships, placements, reachability, rooms, and throne constraints.
/// </summary>
public static class DungeonFloorValidator
{
    public static DungeonFloorValidationResult Validate(DungeonFloor floor, DungeonFloorOptions options)
    {
        if (floor == null) throw new ArgumentNullException(nameof(floor));
        if (options == null) throw new ArgumentNullException(nameof(options));

        var errors = new List<string>();
        void Check(bool condition, string error) { if (!condition) errors.Add(error); }

        // 1. Validate dimensions, seed, and throne flag match options
        Check(floor.Width == options.Width && floor.Height == options.Height,
            "dimensions: Floor dimensions must match options.");
        Check(floor.Seed == options.Seed,
            "seed: Floor seed must match options seed.");
        Check(floor.IsThroneFloor == options.IsThroneFloor,
            "throne: Floor throne flag must match options throne flag.");

        // 2. Validate border cells are all walls (Floor false)
        bool borderValid = true;
        for (int x = 0; x < floor.Width; x++)
        {
            if (floor.Layers[DungeonLayers.Floor][x, 0] || floor.Layers[DungeonLayers.Floor][x, floor.Height - 1])
                borderValid = false;
        }
        for (int y = 1; y < floor.Height - 1; y++)
        {
            if (floor.Layers[DungeonLayers.Floor][0, y] || floor.Layers[DungeonLayers.Floor][floor.Width - 1, y])
                borderValid = false;
        }
        Check(borderValid, "border: All border cells must be walls (Floor false).");

        // 3. Validate Dungeon == !Floor everywhere
        bool dungeonValid = true;
        var floorLayer = floor.Layers[DungeonLayers.Floor];
        var dungeonLayer = floor.Layers[DungeonLayers.Dungeon];
        for (int x = 0; x < floor.Width && dungeonValid; x++)
        {
            for (int y = 0; y < floor.Height && dungeonValid; y++)
            {
                if (dungeonLayer[x, y] != !floorLayer[x, y])
                    dungeonValid = false;
            }
        }
        Check(dungeonValid, "dungeon: Dungeon layer must be the inverse of Floor layer everywhere.");

        // 4. Validate layer subset relationships: Carpet ⊆ Floor, Columns ⊆ Dungeon, Torchlights ⊆ Dungeon
        bool carpetValid = ValidateLayerSubset(floor, DungeonLayers.Carpet, DungeonLayers.Floor);
        bool columnsValid = ValidateLayerSubset(floor, DungeonLayers.Columns, DungeonLayers.Dungeon);
        bool torchlightsValid = ValidateLayerSubset(floor, DungeonLayers.Torchlights, DungeonLayers.Dungeon);
        Check(carpetValid, "carpet: Carpet cells must be a subset of Floor.");
        Check(columnsValid, "columns: Columns cells must be a subset of Dungeon.");
        Check(torchlightsValid, "torchlights: Torchlights cells must be a subset of Dungeon.");

        // 5. Validate Start and Stairs are on Floor and not equal
        bool startOnFloor = floorLayer.At(floor.Start);
        bool stairsOnFloor = floorLayer.At(floor.Stairs);
        bool startNotStairs = !floor.Start.Equals(floor.Stairs);
        Check(startOnFloor, "start: Start position must be on Floor.");
        Check(stairsOnFloor, "stairs: Stairs position must be on Floor.");
        Check(startNotStairs, "start.equals.stairs: Start and Stairs must be different positions.");

        // 6. Validate placements
        var placementCells = new HashSet<GridPoint>();
        ValidatePlacements(floor, options, errors, placementCells);

        // 7. Validate reachability: every Floor cell is reachable from Start
        var reachable = GridSearch.VisitOrder(floor.Start, floor.Neighbors);
        var reachableSet = new HashSet<GridPoint>(reachable);
        int unreachableCount = 0;
        for (int x = 0; x < floor.Width; x++)
        {
            for (int y = 0; y < floor.Height; y++)
            {
                var point = new GridPoint(x, y);
                if (floorLayer[x, y] && !reachableSet.Contains(point))
                    unreachableCount++;
            }
        }
        Check(unreachableCount == 0,
            $"reachability: {unreachableCount} Floor cells are unreachable from Start.");

        // 8. Validate rooms: inside map, all cells on Floor, pairwise non-overlapping
        for (int i = 0; i < floor.Rooms.Count; i++)
        {
            var room = floor.Rooms[i];
            // Check room is inside map
            Check(room.X >= 0 && room.Y >= 0 && room.X + room.Width <= floor.Width && room.Y + room.Height <= floor.Height,
                $"rooms[{i}]: Room must lie completely inside the map.");
            // Check all cells in room are Floor
            bool roomValid = true;
            foreach (var cell in room.Cells())
            {
                if (!floorLayer.At(cell))
                    roomValid = false;
            }
            Check(roomValid, $"rooms[{i}]: All cells in room must be Floor.");

            // Check non-overlapping with other rooms
            for (int j = i + 1; j < floor.Rooms.Count; j++)
            {
                var other = floor.Rooms[j];
                Check(!room.Intersects(other, 0),
                    $"rooms[{i}].overlap: Rooms must be pairwise non-overlapping.");
            }
        }

        // 9. Validate throne constraints if applicable
        if (floor.IsThroneFloor)
        {
            Check(floor.Width == 12 && floor.Height == 12,
                "throne.size: Throne floor must be 12x12.");
            Check(floor.Start.Equals(new GridPoint(6, 4)),
                "throne.start: Throne floor Start must be at (6,4).");
            Check(floor.Stairs.Equals(new GridPoint(6, 9)),
                "throne.stairs: Throne floor Stairs must be at (6,9).");
            Check(floor.Enemies.Count == 0,
                "throne.enemies: Throne floor must have no enemies.");
            Check(floor.Gold.Count == 0,
                "throne.gold: Throne floor must have no gold.");
            Check(floor.Items.Count == 0,
                "throne.items: Throne floor must have no items.");
            Check(floor.Traps.Count == 0,
                "throne.traps: Throne floor must have no traps.");
        }

        // 10. Validate Start and Stairs layers contain exactly one true cell at Start/Stairs
        ValidateMarkerLayer(floor, DungeonLayers.Start, floor.Start, errors);
        ValidateMarkerLayer(floor, DungeonLayers.Stairs, floor.Stairs, errors);

        return new DungeonFloorValidationResult(errors);
    }

    private static bool ValidateLayerSubset(DungeonFloor floor, string subsetLayer, string supersetLayer)
    {
        if (!floor.Layers.ContainsKey(subsetLayer) || !floor.Layers.ContainsKey(supersetLayer))
            return false;

        var subset = floor.Layers[subsetLayer];
        var superset = floor.Layers[supersetLayer];

        for (int x = 0; x < floor.Width; x++)
        {
            for (int y = 0; y < floor.Height; y++)
            {
                if (subset[x, y] && !superset[x, y])
                    return false;
            }
        }

        return true;
    }

    private static void ValidatePlacements(DungeonFloor floor, DungeonFloorOptions options, List<string> errors, HashSet<GridPoint> placementCells)
    {
        var floorLayer = floor.Layers[DungeonLayers.Floor];
        void Check(bool condition, string error) { if (!condition) errors.Add(error); }

        // Validate all placements are on Floor
        foreach (var enemy in floor.Enemies)
        {
            if (!floorLayer.At(enemy.Cell))
                errors.Add("placements: Enemy placement must be on Floor.");
            placementCells.Add(enemy.Cell);
        }
        foreach (var gold in floor.Gold)
        {
            if (!floorLayer.At(gold.Cell))
                errors.Add("placements: Gold placement must be on Floor.");
            placementCells.Add(gold.Cell);
        }
        foreach (var item in floor.Items)
        {
            if (!floorLayer.At(item.Cell))
                errors.Add("placements: Item placement must be on Floor.");
            placementCells.Add(item.Cell);
        }
        foreach (var trap in floor.Traps)
        {
            if (!floorLayer.At(trap.Cell))
                errors.Add("placements: Trap placement must be on Floor.");
            placementCells.Add(trap.Cell);
        }

        // Validate all placement cells are pairwise distinct and differ from Start/Stairs
        if (placementCells.Contains(floor.Start))
            errors.Add("placements.start: Placement cells must not overlap with Start.");
        if (placementCells.Contains(floor.Stairs))
            errors.Add("placements.stairs: Placement cells must not overlap with Stairs.");

        // Count placements and ensure distinct cells
        var allPlacementCells = new List<GridPoint>();
        allPlacementCells.AddRange(floor.Enemies.Select(e => e.Cell));
        allPlacementCells.AddRange(floor.Gold.Select(g => g.Cell));
        allPlacementCells.AddRange(floor.Items.Select(i => i.Cell));
        allPlacementCells.AddRange(floor.Traps.Select(t => t.Cell));

        if (allPlacementCells.Count != new HashSet<GridPoint>(allPlacementCells).Count)
            errors.Add("placements.distinct: All placement cells must be pairwise distinct.");

        // Validate counts match options
        Check(floor.Enemies.Count == options.EnemyCount,
            $"enemies.count: Floor has {floor.Enemies.Count} enemies, expected {options.EnemyCount}.");
        Check(floor.Gold.Count == options.GoldCount,
            $"gold.count: Floor has {floor.Gold.Count} gold, expected {options.GoldCount}.");
        Check(floor.Items.Count == options.ItemCount,
            $"items.count: Floor has {floor.Items.Count} items, expected {options.ItemCount}.");
        Check(floor.Traps.Count == options.TrapCount,
            $"traps.count: Floor has {floor.Traps.Count} traps, expected {options.TrapCount}.");
    }

    private static void ValidateMarkerLayer(DungeonFloor floor, string layerName, GridPoint expectedPosition, List<string> errors)
    {
        if (!floor.Layers.ContainsKey(layerName))
        {
            errors.Add($"{layerName.ToLower()}.layer: Layer {layerName} must exist.");
            return;
        }

        var layer = floor.Layers[layerName];
        int trueCount = 0;
        GridPoint? actualPosition = null;

        for (int x = 0; x < floor.Width; x++)
        {
            for (int y = 0; y < floor.Height; y++)
            {
                if (layer[x, y])
                {
                    trueCount++;
                    actualPosition = new GridPoint(x, y);
                }
            }
        }

        if (trueCount != 1)
            errors.Add($"{layerName.ToLower()}.count: Layer {layerName} must contain exactly one true cell, found {trueCount}.");

        if (actualPosition.HasValue && !actualPosition.Value.Equals(expectedPosition))
            errors.Add($"{layerName.ToLower()}.position: Layer {layerName} must have its true cell at {expectedPosition}, found at {actualPosition.Value}.");
    }
}
