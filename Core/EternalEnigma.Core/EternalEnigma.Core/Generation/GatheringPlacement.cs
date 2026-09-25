using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

/// <summary>Optional loot sites. Never on the Start→Stairs path, never a progression requirement.</summary>
public static class GatheringPlacement
{
    public const int DefaultCount = 3;
    public const int MaxCount = 16;
    public const int MinStartDistance = 3;
    public const uint StreamBase = 900;

    public static IReadOnlyList<GridPoint> RequiredPath(GridLayer floor, GridPoint start, GridPoint stairs)
    {
        if (floor == null) throw new ArgumentNullException(nameof(floor));
        return GridSearch.Path(start, stairs, p => GridSteps.Neighbors(p, floor.At, DiagonalRule.RequireOpenSides));
    }

    public static IReadOnlyList<GatheringSite> Place(GridLayer floor, GridPoint start, GridPoint stairs,
        IEnumerable<GridPoint> occupied, int seed, int count, int attempt = 0)
    {
        // 1. Throw ArgumentNullException if floor or occupied is null; ArgumentOutOfRangeException if count < 0 || count > MaxCount or attempt < 0
        if (floor == null) throw new ArgumentNullException(nameof(floor));
        if (occupied == null) throw new ArgumentNullException(nameof(occupied));
        if (count < 0 || count > MaxCount) throw new ArgumentOutOfRangeException(nameof(count));
        if (attempt < 0) throw new ArgumentOutOfRangeException(nameof(attempt));

        // 2. If count == 0 return an empty read-only list
        if (count == 0)
            return new List<GatheringSite>().AsReadOnly();

        // 3. blocked = new HashSet<GridPoint>(occupied); add every cell of RequiredPath(floor, start, stairs); add start and stairs
        var blocked = new HashSet<GridPoint>(occupied);
        var requiredPath = RequiredPath(floor, start, stairs);
        foreach (var cell in requiredPath)
            blocked.Add(cell);
        blocked.Add(start);
        blocked.Add(stairs);

        // 4. Build candidates (a List<GridPoint>) by iterating for (int x = 1; x < floor.Width - 1; x++) for (int y = 1; y < floor.Height - 1; y++)
        // A cell qualifies when:
        // - floor[x, y] is true
        // - GridSight.IsRoom(floor, cell) is true
        // - it is not in blocked
        // - Math.Max(Math.Abs(cell.X - start.X), Math.Abs(cell.Y - start.Y)) >= MinStartDistance (Chebyshev distance)
        var candidates = new List<GridPoint>();
        for (int x = 1; x < floor.Width - 1; x++)
        {
            for (int y = 1; y < floor.Height - 1; y++)
            {
                var cell = new GridPoint(x, y);
                if (floor[x, y] && GridSight.IsRoom(floor, cell) && !blocked.Contains(cell) &&
                    Math.Max(Math.Abs(cell.X - start.X), Math.Abs(cell.Y - start.Y)) >= MinStartDistance)
                {
                    candidates.Add(cell);
                }
            }
        }

        // 5. var stream = new SeedStream(seed, StreamBase + (uint)attempt);
        var stream = new SeedStream(seed, StreamBase + (uint)attempt);

        // 6. Repeat count times:
        var result = new List<GatheringSite>();
        for (int i = 0; i < count; i++)
        {
            // if candidates.Count == 0 break
            if (candidates.Count == 0)
                break;

            // int index = stream.Range(candidates.Count)
            int index = stream.Range(candidates.Count);
            var cell = candidates[index];

            // var kind = (GatheringKind)stream.Range(3)
            var kind = (GatheringKind)stream.Range(3);

            // int roll = stream.Range(int.MaxValue)
            int roll = stream.Range(int.MaxValue);

            // add new GatheringSite(cell, kind, roll) to the result
            result.Add(new GatheringSite(cell, kind, roll));

            // then remove from candidates every cell whose Chebyshev distance to cell is ≤ 1
            // (use candidates.RemoveAll(...); this removes cell itself too — spacing so sites never touch)
            candidates.RemoveAll(c => Math.Max(Math.Abs(c.X - cell.X), Math.Abs(c.Y - cell.Y)) <= 1);
        }

        // 7. Return result.AsReadOnly()
        return result.AsReadOnly();
    }
}
