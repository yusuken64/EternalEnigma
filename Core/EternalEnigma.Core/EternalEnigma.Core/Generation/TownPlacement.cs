using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

/// <summary>
/// Determines placement positions for buildings in a town, ensuring positions are reachable,
/// not reserved, and properly spaced apart.
/// </summary>
public static class TownPlacement
{
    /// <summary>
    /// Port of CampaignTownCorridor.CompleteBuildingPositions.
    /// 4-way BFS from spawn in GridSteps.Cardinal order (Up, Left, Right, Down) over walkable cells.
    /// </summary>
    public static List<GridPoint> CompleteBuildingPositions(GridLayer walkable, GridLayer? allies, IEnumerable<GridPoint> existing, GridPoint spawn, int required)
    {
        int width = walkable.Width;
        int height = walkable.Height;

        // Helper function to check if a position is walkable
        bool Open(GridPoint p) => walkable.At(p);

        // BFS to find all reachable cells
        var reachable = new HashSet<GridPoint>();
        var candidates = new List<GridPoint>();
        var pending = new Queue<GridPoint>();

        if (Open(spawn))
        {
            pending.Enqueue(spawn);
            reachable.Add(spawn);
        }

        // Use Cardinal directions in order: Up, Left, Right, Down
        var directions = GridSteps.Cardinal;

        while (pending.Count > 0)
        {
            var at = pending.Dequeue();
            candidates.Add(at);

            foreach (var direction in directions)
            {
                var next = new GridPoint(at.X + direction.X, at.Y + direction.Y);
                if (Open(next) && reachable.Add(next))
                {
                    pending.Enqueue(next);
                }
            }
        }

        // Start with existing positions that are reachable and not reserved
        var result = existing
            .Where(p => reachable.Contains(p) && !IsReserved(p, height))
            .Distinct()
            .ToList();

        // Fill shortfall with candidates
        foreach (var cell in candidates)
        {
            if (result.Count >= required) break;

            // Skip if reserved, is spawn, on border, occupied by ally, or too close to existing result
            if (IsReserved(cell, height) ||
                cell.Equals(spawn) ||
                cell.X == 0 || cell.Y == 0 || cell.X == width - 1 || cell.Y == height - 1 ||
                (allies != null && allies.At(cell)) ||
                result.Any(p => Math.Abs(p.X - cell.X) <= 2 && Math.Abs(p.Y - cell.Y) <= 2))
            {
                continue;
            }

            result.Add(cell);
        }

        return result;
    }

    /// <summary>
    /// Checks if a cell is in the reserved southern corridor region.
    /// </summary>
    private static bool IsReserved(GridPoint p, int height) =>
        p.X >= 8 && p.X <= 12 && p.Y >= 0 && p.Y <= height / 2;
}
