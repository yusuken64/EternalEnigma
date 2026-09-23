using EternalEnigma.Core.World;
using EternalEnigma.Core.Validation;
using System.Collections.Generic;

namespace EternalEnigma.Core.Generation;

public static class DungeonFloorGenerator
{
    public static DungeonFloor Generate(DungeonFloorOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        // Throne floors use the fixed template
        if (options.IsThroneFloor)
        {
            var layers = ThroneRoomTemplate.Layers();
            var floor = new DungeonFloor(
                seed: options.Seed,
                isThroneFloor: true,
                layers: layers,
                rooms: new[] { ThroneRoomTemplate.Room },
                start: ThroneRoomTemplate.Start,
                stairs: ThroneRoomTemplate.Stairs,
                enemies: new List<Placement>(),
                gold: new List<Placement>(),
                items: new List<Placement>(),
                traps: new List<Placement>()
            );
            return floor;
        }

        // Normal floors: retry up to 32 attempts
        var diagnostics = new List<string>();
        for (int attempt = 0; attempt < 32; attempt++)
        {
            try
            {
                return Build(options, attempt);
            }
            catch (InvalidOperationException error)
            {
                diagnostics.Add($"Attempt {attempt}: {error.Message}");
            }
        }

        throw new InvalidOperationException($"Seed {options.Seed}: no valid dungeon floor in 32 attempts.\n" + string.Join("\n", diagnostics));
    }

    private static DungeonFloor Build(DungeonFloorOptions options, int attempt)
    {
        int seed = options.Seed;
        int W = options.Width, H = options.Height;

        // Initialize RNG streams
        var layout = new SeedStream(seed, (uint)(600 + attempt));
        var decor = new SeedStream(seed, (uint)(700 + attempt));
        var place = new SeedStream(seed, (uint)(800 + attempt));

        // Step 1: BSP
        var root = new GridRect(1, 1, W - 2, H - 2);
        var leaves = new List<GridRect>();
        BuildBSPTree(root, layout, leaves, depth: 0);

        // Step 2: Place rooms in leaves
        var rooms = new List<GridRect>();
        var floorCells = new bool[W, H];
        foreach (var leaf in leaves)
        {
            int rw = 3 + layout.Range(Math.Max(1, leaf.Width - 4));
            rw = Math.Min(rw, leaf.Width - 2);
            int rh = 3 + layout.Range(Math.Max(1, leaf.Height - 4));
            rh = Math.Min(rh, leaf.Height - 2);

            int rx = leaf.X + 1 + layout.Range(Math.Max(1, leaf.Width - rw - 1));
            int ry = leaf.Y + 1 + layout.Range(Math.Max(1, leaf.Height - rh - 1));

            var room = new GridRect(rx, ry, rw, rh);
            rooms.Add(room);

            // Mark room cells as floor
            foreach (var cell in room.Cells())
            {
                floorCells[cell.X, cell.Y] = true;
            }
        }

        // Step 3: Carve corridors between rooms (recursively by BSP structure)
        CarveCorridors(root, layout, rooms, floorCells, leaves, W, H);

        // Add extra random corridors
        int extraCorridors = layout.Range(3);
        for (int i = 0; i < extraCorridors; i++)
        {
            var room1 = rooms[layout.Range(rooms.Count)];
            var room2 = rooms[layout.Range(rooms.Count)];
            if (room1.Equals(room2)) continue;

            CarveRandomCorridor(layout, floorCells, room1, room2, W, H);
        }

        // Step 4: Connectivity check
        var floorLayer = new GridLayer(floorCells);
        var reachable = GridSearch.VisitOrder(rooms[0].Center, p => GetFloorNeighbors(floorLayer, p));
        var unreachableFloor = new HashSet<GridPoint>();
        for (int x = 1; x < W - 1; x++)
        {
            for (int y = 1; y < H - 1; y++)
            {
                if (floorCells[x, y])
                {
                    var cell = new GridPoint(x, y);
                    if (!reachable.Contains(cell))
                        unreachableFloor.Add(cell);
                }
            }
        }
        if (unreachableFloor.Count > 0)
            throw new InvalidOperationException("Disconnected floor");

        // Step 5: Generate layers
        var layers = GenerateLayers(floorCells, rooms, decor, W, H);

        // Step 6: Generate placements
        var (startPos, stairsPos, enemies, gold, items, traps) =
            GeneratePlacements(options, place, floorLayer, rooms, W, H);

        // Create and return the floor
        var floor = new DungeonFloor(
            seed: seed,
            isThroneFloor: false,
            layers: layers,
            rooms: rooms,
            start: startPos,
            stairs: stairsPos,
            enemies: enemies,
            gold: gold,
            items: items,
            traps: traps
        );

        // Validate before returning
        var validation = DungeonFloorValidator.Validate(floor, options);
        if (!validation.IsValid)
            throw new InvalidOperationException(string.Join("\n", validation.Errors));

        return floor;
    }

    private static void BuildBSPTree(GridRect leaf, SeedStream layout, List<GridRect> leaves, int depth)
    {
        // Check if we should stop splitting
        bool isSmall = leaf.Width < 12 && leaf.Height < 12 && leaf.Width <= 10 && leaf.Height <= 10;
        bool canSplitVertical = leaf.Width >= 12;
        bool canSplitHorizontal = leaf.Height >= 12;

        if (isSmall || (!canSplitVertical && !canSplitHorizontal))
        {
            // This is a final leaf
            leaves.Add(leaf);
            return;
        }

        // Check forced splits (must split because size > 10 on one side)
        if (leaf.Width > 10 && leaf.Height <= 10)
            canSplitVertical = true;
        if (leaf.Height > 10 && leaf.Width <= 10)
            canSplitHorizontal = true;

        // Determine orientation
        bool splitVertical;
        if (canSplitVertical && !canSplitHorizontal)
            splitVertical = true;
        else if (canSplitHorizontal && !canSplitVertical)
            splitVertical = false;
        else if (leaf.Width * 4 > leaf.Height * 5)
            splitVertical = true;
        else if (leaf.Height * 4 > leaf.Width * 5)
            splitVertical = false;
        else
            splitVertical = layout.Range(2) == 0;

        // Check for optional split condition
        if (leaf.Width >= 12 && leaf.Height >= 12 && layout.Range(4) == 0)
        {
            // Don't split, treat as leaf
            leaves.Add(leaf);
            return;
        }

        if (depth >= 8)
        {
            // Max depth reached
            leaves.Add(leaf);
            return;
        }

        // Compute split position
        int cut;
        if (splitVertical)
        {
            if (leaf.Width < 12)
            {
                // Can't split vertically
                leaves.Add(leaf);
                return;
            }
            cut = 6 + layout.Range(leaf.Width - 11);

            var left = new GridRect(leaf.X, leaf.Y, cut, leaf.Height);
            var right = new GridRect(leaf.X + cut, leaf.Y, leaf.Width - cut, leaf.Height);

            BuildBSPTree(left, layout, leaves, depth + 1);
            BuildBSPTree(right, layout, leaves, depth + 1);
        }
        else
        {
            if (leaf.Height < 12)
            {
                // Can't split horizontally
                leaves.Add(leaf);
                return;
            }
            cut = 6 + layout.Range(leaf.Height - 11);

            var top = new GridRect(leaf.X, leaf.Y, leaf.Width, cut);
            var bottom = new GridRect(leaf.X, leaf.Y + cut, leaf.Width, leaf.Height - cut);

            BuildBSPTree(top, layout, leaves, depth + 1);
            BuildBSPTree(bottom, layout, leaves, depth + 1);
        }
    }

    private static void CarveCorridors(GridRect node, SeedStream layout, List<GridRect> rooms, bool[,] floorCells, List<GridRect> leaves, int W, int H)
    {
        // This would be called recursively, but since we're building leaves directly,
        // we'll carve corridors between adjacent rooms in the leaves list
        // For now, we do pairwise carving between rooms from different leaves

        for (int i = 0; i < leaves.Count - 1; i++)
        {
            var leaf1 = leaves[i];
            var leaf2 = leaves[i + 1];

            // Find rooms in each leaf (rooms are in order of leaves)
            var room1 = rooms[i];
            var room2 = rooms[i + 1];

            CarveRandomCorridor(layout, floorCells, room1, room2, W, H);
        }
    }

    private static void CarveRandomCorridor(SeedStream layout, bool[,] floorCells, GridRect room1, GridRect room2, int W, int H)
    {
        // Pick random floor cells from each room
        int x1 = room1.X + layout.Range(room1.Width);
        int y1 = room1.Y + layout.Range(room1.Height);
        int x2 = room2.X + layout.Range(room2.Width);
        int y2 = room2.Y + layout.Range(room2.Height);

        // Decide L-corridor direction
        if (layout.Range(2) == 0)
        {
            // Horizontal first, then vertical
            CarveLine(floorCells, x1, y1, x2, y1, W, H);
            CarveLine(floorCells, x2, y1, x2, y2, W, H);
        }
        else
        {
            // Vertical first, then horizontal
            CarveLine(floorCells, x1, y1, x1, y2, W, H);
            CarveLine(floorCells, x1, y2, x2, y2, W, H);
        }
    }

    private static void CarveLine(bool[,] floorCells, int x1, int y1, int x2, int y2, int W, int H)
    {
        // Carve a horizontal or vertical line of floor
        if (x1 == x2)
        {
            // Vertical line
            int minY = Math.Min(y1, y2);
            int maxY = Math.Max(y1, y2);
            for (int y = minY; y <= maxY; y++)
            {
                int cy = Math.Max(1, Math.Min(H - 2, y));
                int cx = Math.Max(1, Math.Min(W - 2, x1));
                floorCells[cx, cy] = true;
            }
        }
        else
        {
            // Horizontal line
            int minX = Math.Min(x1, x2);
            int maxX = Math.Max(x1, x2);
            for (int x = minX; x <= maxX; x++)
            {
                int cx = Math.Max(1, Math.Min(W - 2, x));
                int cy = Math.Max(1, Math.Min(H - 2, y1));
                floorCells[cx, cy] = true;
            }
        }
    }

    private static IEnumerable<GridPoint> GetFloorNeighbors(GridLayer floorLayer, GridPoint p)
    {
        // Eight-way neighbors with RequireOpenSides rule
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var next = new GridPoint(p.X + dx, p.Y + dy);

                if (!floorLayer.Contains(next) || !floorLayer[next.X, next.Y])
                    continue;

                // RequireOpenSides: diagonals must have both orthogonal neighbors open
                if (dx != 0 && dy != 0)
                {
                    var orthogonal1 = new GridPoint(p.X + dx, p.Y);
                    var orthogonal2 = new GridPoint(p.X, p.Y + dy);
                    if (!floorLayer.Contains(orthogonal1) || !floorLayer[orthogonal1.X, orthogonal1.Y] ||
                        !floorLayer.Contains(orthogonal2) || !floorLayer[orthogonal2.X, orthogonal2.Y])
                        continue;
                }

                yield return next;
            }
        }
    }

    private static Dictionary<string, GridLayer> GenerateLayers(bool[,] floorCells, List<GridRect> rooms, SeedStream decor, int W, int H)
    {
        var dungeonCells = new bool[W, H];
        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++)
            {
                dungeonCells[x, y] = !floorCells[x, y];
            }
        }

        // Carpet: start with floor, erode twice
        var carpetCells = new bool[W, H];
        Array.Copy(floorCells, carpetCells, floorCells.Length);

        // First erosion
        var eroded1 = new bool[W, H];
        for (int x = 1; x < W - 1; x++)
        {
            for (int y = 1; y < H - 1; y++)
            {
                if (!carpetCells[x, y]) continue;

                bool allNeighbors = true;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (!carpetCells[x + dx, y + dy])
                        {
                            allNeighbors = false;
                            break;
                        }
                    }
                    if (!allNeighbors) break;
                }

                eroded1[x, y] = allNeighbors;
            }
        }

        // Second erosion
        var eroded2 = new bool[W, H];
        for (int x = 1; x < W - 1; x++)
        {
            for (int y = 1; y < H - 1; y++)
            {
                if (!eroded1[x, y]) continue;

                bool allNeighbors = true;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (!eroded1[x + dx, y + dy])
                        {
                            allNeighbors = false;
                            break;
                        }
                    }
                    if (!allNeighbors) break;
                }

                eroded2[x, y] = allNeighbors;
            }
        }

        // Carpet = eroded2 AND union of room rects
        var carpetCells2 = new bool[W, H];
        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++)
            {
                if (!eroded2[x, y]) continue;

                var p = new GridPoint(x, y);
                bool inRoom = false;
                foreach (var room in rooms)
                {
                    if (room.Contains(p))
                    {
                        inRoom = true;
                        break;
                    }
                }

                carpetCells2[x, y] = inRoom;
            }
        }

        // Columns: for each room with Width >= 5 && Height >= 5, place at 4 corners
        var columnsCells = new bool[W, H];
        foreach (var room in rooms)
        {
            if (room.Width < 5 || room.Height < 5) continue;

            var corners = new[]
            {
                new GridPoint(room.X - 1, room.Y - 1),
                new GridPoint(room.X + room.Width, room.Y - 1),
                new GridPoint(room.X - 1, room.Y + room.Height),
                new GridPoint(room.X + room.Width, room.Y + room.Height)
            };

            foreach (var corner in corners)
            {
                if (corner.X >= 0 && corner.X < W && corner.Y >= 0 && corner.Y < H &&
                    dungeonCells[corner.X, corner.Y])
                {
                    columnsCells[corner.X, corner.Y] = true;
                }
            }
        }

        // Torchlights: for each room, place torches on edges
        var torchlightsCells = new bool[W, H];
        foreach (var room in rooms)
        {
            int phase = decor.Range(3);

            // Walk each of 4 edges
            // Top edge (y = room.Y - 1)
            if (room.Y - 1 >= 0)
            {
                for (int k = 0; k < room.Width; k++)
                {
                    if ((k + phase) % 3 == 0)
                    {
                        int x = room.X + k;
                        if (x >= 0 && x < W && !IsAdjacentToRoomFloor(x, room.Y - 1, room, floorCells, W, H))
                        {
                            torchlightsCells[x, room.Y - 1] = true;
                        }
                    }
                }
            }

            // Bottom edge (y = room.Y + room.Height)
            if (room.Y + room.Height < H)
            {
                for (int k = 0; k < room.Width; k++)
                {
                    if ((k + phase) % 3 == 0)
                    {
                        int x = room.X + k;
                        if (x >= 0 && x < W && !IsAdjacentToRoomFloor(x, room.Y + room.Height, room, floorCells, W, H))
                        {
                            torchlightsCells[x, room.Y + room.Height] = true;
                        }
                    }
                }
            }

            // Left edge (x = room.X - 1)
            if (room.X - 1 >= 0)
            {
                for (int k = 0; k < room.Height; k++)
                {
                    if ((k + phase) % 3 == 0)
                    {
                        int y = room.Y + k;
                        if (y >= 0 && y < H && !IsAdjacentToRoomFloor(room.X - 1, y, room, floorCells, W, H))
                        {
                            torchlightsCells[room.X - 1, y] = true;
                        }
                    }
                }
            }

            // Right edge (x = room.X + room.Width)
            if (room.X + room.Width < W)
            {
                for (int k = 0; k < room.Height; k++)
                {
                    if ((k + phase) % 3 == 0)
                    {
                        int x = room.X + room.Width;
                        int y = room.Y + k;
                        if (y >= 0 && y < H && !IsAdjacentToRoomFloor(x, y, room, floorCells, W, H))
                        {
                            torchlightsCells[x, y] = true;
                        }
                    }
                }
            }
        }

        return new Dictionary<string, GridLayer>(StringComparer.Ordinal)
        {
            { DungeonLayers.Floor, new GridLayer(floorCells) },
            { DungeonLayers.Dungeon, new GridLayer(dungeonCells) },
            { DungeonLayers.Carpet, new GridLayer(carpetCells2) },
            { DungeonLayers.Columns, new GridLayer(columnsCells) },
            { DungeonLayers.Torchlights, new GridLayer(torchlightsCells) }
        };
    }

    private static bool IsAdjacentToRoomFloor(int x, int y, GridRect room, bool[,] floorCells, int W, int H)
    {
        // Check if orthogonally adjacent to any floor cell outside this room
        var orthogonal = new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) };

        foreach (var (ox, oy) in orthogonal)
        {
            if (ox >= 0 && ox < W && oy >= 0 && oy < H && floorCells[ox, oy])
            {
                // Check if this floor cell is outside the room
                if (!room.Contains(new GridPoint(ox, oy)))
                    return true;
            }
        }

        return false;
    }

    private static (GridPoint start, GridPoint stairs, List<Placement> enemies, List<Placement> gold, List<Placement> items, List<Placement> traps)
        GeneratePlacements(DungeonFloorOptions options, SeedStream place, GridLayer floorLayer, List<GridRect> rooms, int W, int H)
    {
        // Collect all floor cells
        var floorCells = new List<GridPoint>();
        for (int x = 1; x < W - 1; x++)
        {
            for (int y = 1; y < H - 1; y++)
            {
                if (floorLayer[x, y])
                {
                    floorCells.Add(new GridPoint(x, y));
                }
            }
        }

        // Start: uniform pick among floor cells where IsRoom is true
        var roomCells = new List<GridPoint>();
        foreach (var cell in floorCells)
        {
            if (GridSight.IsRoom(floorLayer, cell))
            {
                roomCells.Add(cell);
            }
        }

        if (roomCells.Count == 0)
            throw new InvalidOperationException("No room cells for start placement");

        var startPos = roomCells[place.Range(roomCells.Count)];

        // Stairs: furthest from start, or at least 8 steps away
        var distances = GridSearch.Distances(startPos, p => GetFloorNeighbors(floorLayer, p));
        int maxDist = Math.Max(8, (W + H) / 6);

        GridPoint? stairsPos = null;
        int stairsDist = -1;

        foreach (var cell in floorCells)
        {
            if (!distances.TryGetValue(cell, out int dist)) continue;
            if (dist < maxDist) continue;
            if (dist > stairsDist)
            {
                stairsDist = dist;
                stairsPos = cell;
            }
        }

        // If no cell satisfies the distance requirement, pick the furthest
        if (stairsPos == null)
        {
            foreach (var cell in floorCells)
            {
                if (!distances.TryGetValue(cell, out int dist)) continue;
                if (dist > stairsDist)
                {
                    stairsDist = dist;
                    stairsPos = cell;
                }
            }
        }

        if (!stairsPos.HasValue || stairsPos.Value.Equals(startPos))
            throw new InvalidOperationException("Could not place stairs");

        var occupied = new HashSet<GridPoint> { startPos, stairsPos.Value };

        // Enemies
        var enemies = new List<Placement>();
        for (int i = 0; i < options.EnemyCount; i++)
        {
            var candidates = new List<GridPoint>();
            foreach (var cell in floorCells)
            {
                if (occupied.Contains(cell)) continue;

                // Chebyshev distance to start > 3
                int chebDist = Math.Max(Math.Abs(cell.X - startPos.X), Math.Abs(cell.Y - startPos.Y));
                if (chebDist > 3)
                {
                    candidates.Add(cell);
                }
            }

            if (candidates.Count == 0)
                throw new InvalidOperationException("Not enough enemy cells");

            var chosen = candidates[place.Range(candidates.Count)];
            int roll = place.Range(int.MaxValue);
            enemies.Add(new Placement(chosen, roll));
            occupied.Add(chosen);
        }

        // Gold
        var gold = new List<Placement>();
        for (int i = 0; i < options.GoldCount; i++)
        {
            var candidates = new List<GridPoint>();
            foreach (var cell in floorCells)
            {
                if (!occupied.Contains(cell))
                    candidates.Add(cell);
            }

            if (candidates.Count == 0)
                break;

            var chosen = candidates[place.Range(candidates.Count)];
            int roll = place.Range(int.MaxValue);
            gold.Add(new Placement(chosen, roll));
            occupied.Add(chosen);
        }

        // Items
        var items = new List<Placement>();
        for (int i = 0; i < options.ItemCount; i++)
        {
            var candidates = new List<GridPoint>();
            foreach (var cell in floorCells)
            {
                if (!occupied.Contains(cell))
                    candidates.Add(cell);
            }

            if (candidates.Count == 0)
                break;

            var chosen = candidates[place.Range(candidates.Count)];
            int roll = place.Range(int.MaxValue);
            items.Add(new Placement(chosen, roll));
            occupied.Add(chosen);
        }

        // Traps
        var traps = new List<Placement>();
        for (int i = 0; i < options.TrapCount; i++)
        {
            var candidates = new List<GridPoint>();
            foreach (var cell in floorCells)
            {
                if (!occupied.Contains(cell))
                    candidates.Add(cell);
            }

            if (candidates.Count == 0)
                break;

            var chosen = candidates[place.Range(candidates.Count)];
            int roll = place.Range(int.MaxValue);
            traps.Add(new Placement(chosen, roll));
            occupied.Add(chosen);
        }

        return (startPos, stairsPos.Value, enemies, gold, items, traps);
    }
}
