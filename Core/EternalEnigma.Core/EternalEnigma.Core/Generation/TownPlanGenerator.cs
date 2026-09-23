using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using EternalEnigma.Core.Validation;

namespace EternalEnigma.Core.Generation;

/// <summary>
/// Generates deterministic procedural towns with buildings, roads, trees, allies, and shop interiors.
/// Uses a 32-attempt retry/diagnostics pattern to ensure valid generation.
/// </summary>
public static class TownPlanGenerator
{
    private const int SpineX = 10;
    private const int MaxAttempts = 32;

    public static TownPlan Generate(TownPlanOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        var diagnostics = new List<string>();
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            try
            {
                return GenerateAttempt(options, attempt);
            }
            catch (InvalidOperationException error)
            {
                diagnostics.Add($"Attempt {attempt}: {error.Message}");
            }
        }

        throw new InvalidOperationException(
            $"Seed {options.Seed}: no valid town generation in {MaxAttempts} attempts.\n" +
            string.Join("\n", diagnostics));
    }

    private static TownPlan GenerateAttempt(TownPlanOptions options, int attempt)
    {
        int W = options.Width;
        int H = options.Height;
        int seed = options.Seed;

        // Initialize random streams
        var plots = new SeedStream(seed, 900u + (uint)attempt);
        var scenery = new SeedStream(seed, 1000u + (uint)attempt);
        var allies = new SeedStream(seed, 1100u + (uint)attempt);

        // Initialize layers as working arrays
        var layers = new Dictionary<string, bool[,]>
        {
            { TownLayers.Roads, new bool[W, H] },
            { TownLayers.Houses, new bool[W, H] },
            { TownLayers.Trees, new bool[W, H] },
            { TownLayers.Parks, new bool[W, H] },
            { TownLayers.Roofs, new bool[W, H] },
            { TownLayers.Buildings, new bool[W, H] },
            { TownLayers.Allies, new bool[W, H] },
            { TownLayers.Dungeon, new bool[W, H] },
            { TownLayers.ShopFloor, new bool[W, H] },
            { TownLayers.ShopWalls, new bool[W, H] },
            { TownLayers.Walkable, new bool[W, H] }
        };

        // Step 1: Generate building plots
        var (acceptedDoors, acceptedBodies) = GeneratePlots(options, layers, plots, W, H);

        // Step 2: Slots and shops
        // Rebuild Buildings layer to ensure doors are only those we actually accepted
        var buildingsArrayForRooms = new bool[W, H];
        foreach (var door in acceptedDoors)
        {
            if (door.X >= 0 && door.X < W && door.Y >= 0 && door.Y < H)
                buildingsArrayForRooms[door.X, door.Y] = true;
        }

        var buildingSlots = CollectBuildingSlotsRaster(acceptedDoors, W, H);
        var buildingsLayer = new GridLayer(buildingsArrayForRooms);
        // ShopInteriors scans doors in x-outer/y-inner order, but options.ShopFlags is indexed by
        // buildingSlots' y-outer/x-inner raster order; remap flags to the scan order it expects.
        var doorsInScanOrder = new List<GridPoint>();
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                if (buildingsArrayForRooms[x, y]) doorsInScanOrder.Add(new GridPoint(x, y));
        var shopFlagsInScanOrder = doorsInScanOrder
            .Select(door => { int slotIndex = buildingSlots.IndexOf(door); return slotIndex >= 0 && slotIndex < options.ShopFlags.Count && options.ShopFlags[slotIndex]; })
            .ToList();
        var rooms = ShopInteriors.ComputeRooms(buildingsLayer, null, shopFlagsInScanOrder, W, H);

        // Clear Houses and set shop layers for each room
        foreach (var room in rooms)
        {
            foreach (var cell in room.Floor)
            {
                if (cell.X >= 0 && cell.X < W && cell.Y >= 0 && cell.Y < H)
                {
                    layers[TownLayers.Houses][cell.X, cell.Y] = false;
                    layers[TownLayers.ShopFloor][cell.X, cell.Y] = true;
                }
            }
            foreach (var cell in room.Wall)
            {
                if (cell.X >= 0 && cell.X < W && cell.Y >= 0 && cell.Y < H)
                {
                    layers[TownLayers.Houses][cell.X, cell.Y] = false;
                    layers[TownLayers.ShopWalls][cell.X, cell.Y] = true;
                }
            }
        }

        // Step 3: Generate roads
        GenerateRoads(layers, acceptedDoors, W, H);

        // Step 4: Trees and parks
        GenerateTreesAndParks(layers, scenery, W, H, options);

        // Step 5: Generate allies
        var allySlots = GenerateAllies(layers, allies, W, H, options);

        // Step 6: Set remaining layers
        layers[TownLayers.Dungeon][SpineX, H - 2] = true;
        Array.Copy(layers[TownLayers.Houses], layers[TownLayers.Roofs], layers[TownLayers.Houses].Length);

        // Compute Walkable layer: !(Houses || Trees || ShopWalls)
        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++)
            {
                bool blocked = layers[TownLayers.Houses][x, y] ||
                              layers[TownLayers.Trees][x, y] ||
                              layers[TownLayers.ShopWalls][x, y];
                layers[TownLayers.Walkable][x, y] = !blocked;
            }
        }

        // Create GridLayers
        var gridLayers = new Dictionary<string, GridLayer>();
        foreach (var kvp in layers)
        {
            gridLayers[kvp.Key] = new GridLayer(kvp.Value);
        }

        // Create TownPlan
        var plan = new TownPlan(
            seed,
            gridLayers,
            buildingSlots,
            allySlots,
            rooms,
            options.PartySpawn,
            options.Exit,
            new GridPoint(SpineX, H - 2)
        );

        // Validate
        var validation = TownPlanValidator.Validate(plan, options);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"Generated plan is invalid: {string.Join("; ", validation.Errors)}");
        }

        return plan;
    }

    private static (List<GridPoint> doors, List<GridRect> bodies) GeneratePlots(
        TownPlanOptions options,
        Dictionary<string, bool[,]> layers,
        SeedStream plots,
        int W, int H)
    {
        var acceptedDoors = new List<GridPoint>();
        var acceptedBodies = new List<GridRect>();

        // Generate candidates
        var candidates = new List<(GridPoint door, GridRect body)>();
        for (int dx = 1; dx <= W - 2; dx++)
        {
            for (int dy = 1; dy <= H - 2; dy++)
            {
                var door = new GridPoint(dx, dy);
                var body = new GridRect(dx - 1, dy + 1, 3, 4);

                // Check if body is fully inside bounds
                if (body.X < 0 || body.Y < 0 || body.Right >= W || body.Top >= H)
                    continue;

                // Check if door is reserved or equals PartySpawn/Exit
                if (TownPlan.IsReservedCorridor(door, H))
                    continue;
                if (door.Equals(options.PartySpawn) || door.Equals(options.Exit))
                    continue;

                // Check if any body cell is reserved
                bool bodyReserved = false;
                foreach (var cell in body.Cells())
                {
                    if (TownPlan.IsReservedCorridor(cell, H))
                    {
                        bodyReserved = true;
                        break;
                    }
                }
                if (bodyReserved)
                    continue;

                // Check if any body cell has X == spineX
                bool bodyOnSpine = false;
                foreach (var cell in body.Cells())
                {
                    if (cell.X == SpineX)
                    {
                        bodyOnSpine = true;
                        break;
                    }
                }
                if (bodyOnSpine)
                    continue;

                // Check if body contains PartySpawn or Exit
                if (body.Contains(options.PartySpawn) || body.Contains(options.Exit))
                    continue;

                candidates.Add((door, body));
            }
        }

        // Shuffle candidates
        var shuffledCandidates = plots.Shuffle(candidates);

        // Greedily accept candidates
        foreach (var (door, body) in shuffledCandidates)
        {
            if (acceptedDoors.Count >= options.BuildingCount)
                break;

            // Check body doesn't intersect with margin
            bool intersects = false;
            foreach (var acceptedBody in acceptedBodies)
            {
                if (body.Intersects(acceptedBody, margin: 1))
                {
                    intersects = true;
                    break;
                }
            }
            if (intersects)
                continue;

            // Check Chebyshev distance >= 3 from other doors
            bool tooClose = false;
            foreach (var acceptedDoor in acceptedDoors)
            {
                int chebyshev = Math.Max(Math.Abs(door.X - acceptedDoor.X), Math.Abs(door.Y - acceptedDoor.Y));
                if (chebyshev < 3)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose)
                continue;

            // Check not 4-adjacent to any accepted body cell
            bool adjacent = false;
            var neighbors = new[] { new GridPoint(door.X - 1, door.Y), new GridPoint(door.X + 1, door.Y),
                                   new GridPoint(door.X, door.Y - 1), new GridPoint(door.X, door.Y + 1) };
            foreach (var neighbor in neighbors)
            {
                foreach (var acceptedBody in acceptedBodies)
                {
                    if (acceptedBody.Contains(neighbor))
                    {
                        adjacent = true;
                        break;
                    }
                }
                if (adjacent) break;
            }
            if (adjacent)
                continue;

            // Accept this building
            acceptedDoors.Add(door);
            acceptedBodies.Add(body);
            layers[TownLayers.Buildings][door.X, door.Y] = true;

            // Mark body as Houses
            foreach (var cell in body.Cells())
            {
                layers[TownLayers.Houses][cell.X, cell.Y] = true;
            }
        }

        // If short, use CompleteBuildingPositions
        if (acceptedDoors.Count < options.BuildingCount)
        {
            // Compute walkable so far (cells not in any body)
            var walkableSoFar = new bool[W, H];
            for (int x = 0; x < W; x++)
            {
                for (int y = 0; y < H; y++)
                {
                    walkableSoFar[x, y] = !layers[TownLayers.Houses][x, y] &&
                                         !layers[TownLayers.ShopWalls][x, y] &&
                                         !layers[TownLayers.Buildings][x, y];
                }
            }

            var walkableLayer = new GridLayer(walkableSoFar);
            var completedDoors = TownPlacement.CompleteBuildingPositions(
                walkableLayer,
                null,
                acceptedDoors,
                options.PartySpawn,
                options.BuildingCount);

            // Add new doors (without bodies)
            foreach (var door in completedDoors)
            {
                if (!acceptedDoors.Contains(door))
                {
                    acceptedDoors.Add(door);
                    layers[TownLayers.Buildings][door.X, door.Y] = true;
                }
            }
        }

        if (acceptedDoors.Count < options.BuildingCount)
        {
            throw new InvalidOperationException(
                $"Could not place {options.BuildingCount} buildings; only placed {acceptedDoors.Count}");
        }

        return (acceptedDoors, acceptedBodies);
    }

    private static List<GridPoint> CollectBuildingSlotsRaster(List<GridPoint> doors, int W, int H)
    {
        var slots = new List<GridPoint>();
        // Iterate y-outer, x-inner (standard raster order) to match TownPlan validation
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                var p = new GridPoint(x, y);
                if (doors.Contains(p))
                    slots.Add(p);
            }
        }
        return slots;
    }

    private static void GenerateRoads(Dictionary<string, bool[,]> layers, List<GridPoint> doors, int W, int H)
    {
        // Set spine vertical line
        for (int y = 0; y < H; y++)
        {
            layers[TownLayers.Roads][SpineX, y] = true;
        }

        // For each door, find path to first road cell
        foreach (var door in doors)
        {
            // Define blocked predicate
            Func<GridPoint, bool> blocked = (GridPoint c) =>
            {
                if (c.X < 0 || c.X >= W || c.Y < 0 || c.Y >= H)
                    return true;
                return (layers[TownLayers.Houses][c.X, c.Y] ||
                       layers[TownLayers.ShopWalls][c.X, c.Y] ||
                       (layers[TownLayers.Buildings][c.X, c.Y] && !c.Equals(door)));
            };

            // 4-way neighbors over !blocked cells
            Func<GridPoint, IEnumerable<GridPoint>> neighbors = (GridPoint c) =>
            {
                var result = new List<GridPoint>();
                var directions = new[] { new GridPoint(0, 1), new GridPoint(-1, 0),
                                        new GridPoint(1, 0), new GridPoint(0, -1) };
                foreach (var dir in directions)
                {
                    var next = new GridPoint(c.X + dir.X, c.Y + dir.Y);
                    if (!blocked(next))
                        result.Add(next);
                }
                return result;
            };

            // Find first road cell using BFS (Nearest)
            var targetRoad = GridSearch.Nearest(door, neighbors, c => c.X >= 0 && c.X < W && c.Y >= 0 && c.Y < H && layers[TownLayers.Roads][c.X, c.Y]);

            if (targetRoad == null)
                throw new InvalidOperationException($"No road reachable from door {door}");

            // Get path
            var path = GridSearch.Path(door, targetRoad.Value, neighbors);

            // Mark all path cells except door as Roads
            foreach (var cell in path)
            {
                if (!cell.Equals(door))
                {
                    layers[TownLayers.Roads][cell.X, cell.Y] = true;
                }
            }
        }

        // Avenue: horizontal line at y = H/2 + 1
        int avenueY = H / 2 + 1;
        for (int x = 1; x < W - 1; x++)
        {
            if (!layers[TownLayers.Houses][x, avenueY] &&
                !layers[TownLayers.ShopWalls][x, avenueY] &&
                !layers[TownLayers.Buildings][x, avenueY])
            {
                layers[TownLayers.Roads][x, avenueY] = true;
            }
        }
    }

    private static void GenerateTreesAndParks(Dictionary<string, bool[,]> layers, SeedStream scenery, int W, int H, TownPlanOptions options)
    {
        // Trees
        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++)
            {
                if (layers[TownLayers.Roads][x, y] || layers[TownLayers.Houses][x, y] ||
                    layers[TownLayers.ShopFloor][x, y] || layers[TownLayers.ShopWalls][x, y] ||
                    layers[TownLayers.Buildings][x, y])
                    continue;

                if (TownPlan.IsReservedCorridor(new GridPoint(x, y), H))
                    continue;

                if (new GridPoint(x, y).Equals(options.PartySpawn) || new GridPoint(x, y).Equals(options.Exit))
                    continue;

                // Check not 4-adjacent to a door (buildings)
                bool adjacentToDoor = false;
                var neighbors = new[] { new GridPoint(x - 1, y), new GridPoint(x + 1, y),
                                       new GridPoint(x, y - 1), new GridPoint(x, y + 1) };
                foreach (var neighbor in neighbors)
                {
                    if (neighbor.X >= 0 && neighbor.X < W && neighbor.Y >= 0 && neighbor.Y < H &&
                        layers[TownLayers.Buildings][neighbor.X, neighbor.Y])
                    {
                        adjacentToDoor = true;
                        break;
                    }
                }
                if (adjacentToDoor)
                    continue;

                if (scenery.Range(100) < 18)
                {
                    layers[TownLayers.Trees][x, y] = true;
                }
            }
        }

        // Parks
        for (int attempt = 0; attempt < 2; attempt++)
        {
            int size = 2 + scenery.Range(2);
            int x = 1 + scenery.Range(W - 1 - size);
            int y = 1 + scenery.Range(H - 1 - size);

            var rect = new GridRect(x, y, size, size);

            // Check if all cells are valid
            bool valid = true;
            foreach (var cell in rect.Cells())
            {
                if (layers[TownLayers.Roads][cell.X, cell.Y] ||
                    layers[TownLayers.Houses][cell.X, cell.Y] ||
                    layers[TownLayers.Trees][cell.X, cell.Y] ||
                    layers[TownLayers.ShopFloor][cell.X, cell.Y] ||
                    layers[TownLayers.ShopWalls][cell.X, cell.Y] ||
                    layers[TownLayers.Buildings][cell.X, cell.Y])
                {
                    valid = false;
                    break;
                }

                if (TownPlan.IsReservedCorridor(cell, H))
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
            {
                foreach (var cell in rect.Cells())
                {
                    layers[TownLayers.Parks][cell.X, cell.Y] = true;
                }
            }
        }
    }

    private static List<Placement> GenerateAllies(Dictionary<string, bool[,]> layers, SeedStream allies, int W, int H, TownPlanOptions options)
    {
        var candidates = new List<GridPoint>();

        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++)
            {
                var cell = new GridPoint(x, y);

                if (layers[TownLayers.Houses][x, y] || layers[TownLayers.Trees][x, y] ||
                    layers[TownLayers.ShopWalls][x, y] || layers[TownLayers.Roads][x, y] ||
                    layers[TownLayers.Buildings][x, y] || layers[TownLayers.ShopFloor][x, y])
                    continue;

                if (TownPlan.IsReservedCorridor(cell, H))
                    continue;

                if (cell.Equals(options.PartySpawn) || cell.Equals(options.Exit) ||
                    cell.Equals(new GridPoint(10, H - 2)))
                    continue;

                // Check not 4-adjacent to a door
                bool adjacentToDoor = false;
                var neighbors = new[] { new GridPoint(x - 1, y), new GridPoint(x + 1, y),
                                       new GridPoint(x, y - 1), new GridPoint(x, y + 1) };
                foreach (var neighbor in neighbors)
                {
                    if (neighbor.X >= 0 && neighbor.X < W && neighbor.Y >= 0 && neighbor.Y < H &&
                        layers[TownLayers.Buildings][neighbor.X, neighbor.Y])
                    {
                        adjacentToDoor = true;
                        break;
                    }
                }
                if (adjacentToDoor)
                    continue;

                // Check 4-adjacent to a road
                bool adjacentToRoad = false;
                foreach (var neighbor in neighbors)
                {
                    if (neighbor.X >= 0 && neighbor.X < W && neighbor.Y >= 0 && neighbor.Y < H &&
                        layers[TownLayers.Roads][neighbor.X, neighbor.Y])
                    {
                        adjacentToRoad = true;
                        break;
                    }
                }
                if (!adjacentToRoad)
                    continue;

                candidates.Add(cell);
            }
        }

        // Shuffle candidates
        var shuffledCandidates = allies.Shuffle(candidates);

        // Accept while pairwise Chebyshev >= 2
        var accepted = new List<GridPoint>();
        foreach (var candidate in shuffledCandidates)
        {
            if (accepted.Count >= options.AllyCount)
                break;

            bool tooClose = false;
            foreach (var other in accepted)
            {
                int chebyshev = Math.Max(Math.Abs(candidate.X - other.X), Math.Abs(candidate.Y - other.Y));
                if (chebyshev < 2)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                accepted.Add(candidate);
                layers[TownLayers.Allies][candidate.X, candidate.Y] = true;
            }
        }

        if (accepted.Count < options.AllyCount)
        {
            throw new InvalidOperationException(
                $"Could not place {options.AllyCount} allies; only placed {accepted.Count}");
        }

        // Sort in raster order and create placements with rolls
        var sorted = new List<GridPoint>();
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                var p = new GridPoint(x, y);
                if (accepted.Contains(p))
                    sorted.Add(p);
            }
        }

        var placements = new List<Placement>();
        foreach (var cell in sorted)
        {
            int roll = allies.Range(int.MaxValue);
            placements.Add(new Placement(cell, roll));
        }

        return placements;
    }

    private static GridLayer ToGridLayer(bool[,] array)
    {
        return new GridLayer(array);
    }
}
