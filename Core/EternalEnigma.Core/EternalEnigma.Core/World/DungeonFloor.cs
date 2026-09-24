using System.Collections.ObjectModel;

namespace EternalEnigma.Core.World;

/// <summary>Immutable, validated result type of dungeon-floor generation.</summary>
public sealed class DungeonFloor
{
    public const int GenerationVersion = 2;
    public int Width { get; }
    public int Height { get; }
    public int Seed { get; }
    public bool IsThroneFloor { get; }
    public IReadOnlyDictionary<string, GridLayer> Layers { get; }
    public IReadOnlyList<GridRect> Rooms { get; }
    public GridPoint Start { get; }
    public GridPoint Stairs { get; }
    public IReadOnlyList<Placement> Enemies { get; }
    public IReadOnlyList<Placement> Gold { get; }
    public IReadOnlyList<Placement> Items { get; }
    public IReadOnlyList<Placement> Traps { get; }
    public IReadOnlyList<GatheringSite> GatheringSites { get; }

    internal DungeonFloor(int seed, bool isThroneFloor, IDictionary<string, GridLayer> layers, IEnumerable<GridRect> rooms,
        GridPoint start, GridPoint stairs, IEnumerable<Placement> enemies, IEnumerable<Placement> gold,
        IEnumerable<Placement> items, IEnumerable<Placement> traps, IEnumerable<GatheringSite>? gatheringSites = null)
    {
        // Validate layers
        if (!layers.ContainsKey(DungeonLayers.Floor))
            throw new ArgumentException($"Layers must contain '{DungeonLayers.Floor}'.");
        if (!layers.ContainsKey(DungeonLayers.Dungeon))
            throw new ArgumentException($"Layers must contain '{DungeonLayers.Dungeon}'.");

        // Wrap the dictionary to ensure StringComparer.Ordinal
        var wrappedLayers = new Dictionary<string, GridLayer>(layers, StringComparer.Ordinal);

        var floorLayer = wrappedLayers[DungeonLayers.Floor];
        var dungeonLayer = wrappedLayers[DungeonLayers.Dungeon];

        // Validate all layers have same dimensions
        Width = floorLayer.Width;
        Height = floorLayer.Height;

        foreach (var layer in wrappedLayers.Values)
        {
            if (layer.Width != Width || layer.Height != Height)
                throw new ArgumentException("All grid layers must have the same dimensions.");
        }

        // Validate Dungeon == !Floor for every cell
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                bool floorCell = floorLayer[x, y];
                bool dungeonCell = dungeonLayer[x, y];
                if (dungeonCell != !floorCell)
                    throw new ArgumentException("Dungeon layer must be the inverse of Floor layer for every cell.");
            }
        }

        // Validate Start and Stairs are on Floor
        if (!floorLayer.At(start))
            throw new ArgumentException($"Start position {start} must be on the Floor layer.");
        if (!floorLayer.At(stairs))
            throw new ArgumentException($"Stairs position {stairs} must be on the Floor layer.");

        // Add Start and Stairs layers if missing
        if (!wrappedLayers.ContainsKey(DungeonLayers.Start))
        {
            var startCells = new bool[Width, Height];
            startCells[start.X, start.Y] = true;
            wrappedLayers[DungeonLayers.Start] = new GridLayer(startCells);
        }

        if (!wrappedLayers.ContainsKey(DungeonLayers.Stairs))
        {
            var stairsCells = new bool[Width, Height];
            stairsCells[stairs.X, stairs.Y] = true;
            wrappedLayers[DungeonLayers.Stairs] = new GridLayer(stairsCells);
        }

        Seed = seed;
        IsThroneFloor = isThroneFloor;
        Layers = new ReadOnlyDictionary<string, GridLayer>(wrappedLayers);
        Rooms = Array.AsReadOnly(rooms.ToArray());
        Start = start;
        Stairs = stairs;
        Enemies = Array.AsReadOnly(enemies.ToArray());
        Gold = Array.AsReadOnly(gold.ToArray());
        Items = Array.AsReadOnly(items.ToArray());
        Traps = Array.AsReadOnly(traps.ToArray());
        GatheringSites = Array.AsReadOnly((gatheringSites ?? Enumerable.Empty<GatheringSite>()).ToArray());
    }

    /// <summary>Checks if a cell is within bounds.</summary>
    public bool Contains(GridPoint cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;

    /// <summary>Checks if a cell is walkable (on the Floor layer).</summary>
    public bool IsWalkable(GridPoint cell) => Layers[DungeonLayers.Floor].At(cell);

    /// <summary>Determines if a step from one point to another is valid (RequireOpenSides rule).</summary>
    public bool CanStep(GridPoint from, GridPoint to) => GridSteps.CanStep(from, to, IsWalkable, DiagonalRule.RequireOpenSides);

    /// <summary>Returns valid neighboring cells (EightWay order, RequireOpenSides rule).</summary>
    public IEnumerable<GridPoint> Neighbors(GridPoint from) => GridSteps.Neighbors(from, IsWalkable, DiagonalRule.RequireOpenSides);

    /// <summary>Checks if a cell is in a room (open 2x2 area).</summary>
    public bool IsRoom(GridPoint cell) => GridSight.IsRoom(Layers[DungeonLayers.Floor], cell);

    /// <summary>TileWorldDungeon sight radius rule: 8 in a room, otherwise 2 for a 3x3 footprint or 1.</summary>
    public int SightRadius(GridPoint origin, bool largeFootprint = false) => IsRoom(origin) ? 8 : largeFootprint ? 2 : 1;

    /// <summary>Returns tiles visible from origin within radius using line-of-sight.</summary>
    public HashSet<GridPoint> VisibleTiles(GridPoint origin, int radius) => GridSight.VisibleTiles(Layers[DungeonLayers.Floor], origin, radius);

    /// <summary>Nearest walkable cell (BFS, RequireOpenSides, origin first) with isFree(cell) true; returns origin if origin is not walkable or nothing qualifies.</summary>
    public GridPoint NearestFree(GridPoint origin, Func<GridPoint, bool> isFree)
    {
        if (!IsWalkable(origin) || isFree(origin))
            return origin;

        var result = GridSearch.Nearest(origin, Neighbors, isFree);
        return result ?? origin;
    }

    /// <summary>Up to `count` distinct nearest free cells not in `occupied` (BFS order), excluding origin.</summary>
    public IReadOnlyList<GridPoint> NearestFreeCells(GridPoint origin, int count, ISet<GridPoint> occupied)
    {
        var result = new List<GridPoint>();
        var visited = new HashSet<GridPoint> { origin };

        var queue = new Queue<GridPoint>();
        queue.Enqueue(origin);

        while (queue.Count > 0 && result.Count < count)
        {
            var current = queue.Dequeue();

            foreach (var next in Neighbors(current))
            {
                if (!visited.Contains(next))
                {
                    visited.Add(next);

                    if (!occupied.Contains(next))
                    {
                        result.Add(next);
                        if (result.Count >= count)
                            break;
                    }

                    queue.Enqueue(next);
                }
            }
        }

        return result.AsReadOnly();
    }

    /// <summary>BFS steps between two walkable cells, or -1 if unreachable.</summary>
    public int Distance(GridPoint from, GridPoint to)
    {
        var distances = GridSearch.Distances(from, Neighbors);
        return distances.TryGetValue(to, out var distance) ? distance : -1;
    }
}
