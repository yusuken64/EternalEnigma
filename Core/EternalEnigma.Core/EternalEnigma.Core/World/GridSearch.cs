namespace EternalEnigma.Core.World;

public static class GridSearch
{
    /// <summary>BFS visit order from origin (origin first). `neighbors` supplies already-filtered successors. Deterministic given neighbor order.</summary>
    public static IReadOnlyList<GridPoint> VisitOrder(GridPoint origin, Func<GridPoint, IEnumerable<GridPoint>> neighbors)
    {
        var visited = new HashSet<GridPoint>();
        var queue = new Queue<GridPoint>();
        var result = new List<GridPoint>();

        queue.Enqueue(origin);
        visited.Add(origin);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            foreach (var next in neighbors(current))
            {
                if (!visited.Contains(next))
                {
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
        }

        return result.AsReadOnly();
    }

    /// <summary>First cell in BFS order satisfying isTarget (origin included), or null.</summary>
    public static GridPoint? Nearest(GridPoint origin, Func<GridPoint, IEnumerable<GridPoint>> neighbors, Func<GridPoint, bool> isTarget)
    {
        if (isTarget(origin))
            return origin;

        var visited = new HashSet<GridPoint>();
        var queue = new Queue<GridPoint>();

        queue.Enqueue(origin);
        visited.Add(origin);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var next in neighbors(current))
            {
                if (!visited.Contains(next))
                {
                    if (isTarget(next))
                        return next;

                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
        }

        return null;
    }

    /// <summary>BFS distances (steps) from origin to every reachable cell, origin => 0.</summary>
    public static Dictionary<GridPoint, int> Distances(GridPoint origin, Func<GridPoint, IEnumerable<GridPoint>> neighbors)
    {
        var distances = new Dictionary<GridPoint, int>();
        var queue = new Queue<GridPoint>();

        queue.Enqueue(origin);
        distances[origin] = 0;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            int currentDist = distances[current];

            foreach (var next in neighbors(current))
            {
                if (!distances.ContainsKey(next))
                {
                    distances[next] = currentDist + 1;
                    queue.Enqueue(next);
                }
            }
        }

        return distances;
    }

    /// <summary>Shortest path origin..target inclusive (BFS parents), or empty list if unreachable.</summary>
    public static IReadOnlyList<GridPoint> Path(GridPoint origin, GridPoint target, Func<GridPoint, IEnumerable<GridPoint>> neighbors)
    {
        if (origin.Equals(target))
            return new List<GridPoint> { origin }.AsReadOnly();

        var visited = new HashSet<GridPoint>();
        var queue = new Queue<GridPoint>();
        var parents = new Dictionary<GridPoint, GridPoint>();

        queue.Enqueue(origin);
        visited.Add(origin);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current.Equals(target))
            {
                // Reconstruct path
                var path = new List<GridPoint>();
                var node = target;
                while (!node.Equals(origin))
                {
                    path.Add(node);
                    node = parents[node];
                }
                path.Add(origin);
                path.Reverse();
                return path.AsReadOnly();
            }

            foreach (var next in neighbors(current))
            {
                if (!visited.Contains(next))
                {
                    visited.Add(next);
                    parents[next] = current;
                    queue.Enqueue(next);
                }
            }
        }

        return new List<GridPoint>().AsReadOnly();
    }
}
