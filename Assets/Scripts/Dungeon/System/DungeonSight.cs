using System.Collections.Generic;
using UnityEngine;

// Deterministic tile sight. Walls are visible endpoints but block everything behind them.
internal static class DungeonSight
{
    internal static bool OverlapsVisible(HashSet<Vector3Int> tiles, BoundsInt bounds)
    {
        foreach (var cell in bounds.allPositionsWithin)
            if (tiles.Contains(cell)) return true;
        return false;
    }

    internal static bool IsRoom(bool[,] terrain, Vector3Int origin)
    {
        // A room tile belongs to an open 2x2 area. One-tile passages remain corridors.
        for (int dx = -1; dx <= 0; dx++)
        for (int dy = -1; dy <= 0; dy++)
        {
            var corner = origin + new Vector3Int(dx, dy, 0);
            if (GridMovement.IsWalkable(terrain, corner) &&
                GridMovement.IsWalkable(terrain, corner + Vector3Int.right) &&
                GridMovement.IsWalkable(terrain, corner + Vector3Int.up) &&
                GridMovement.IsWalkable(terrain, corner + new Vector3Int(1, 1, 0))) return true;
        }
        return false;
    }

    internal static HashSet<Vector3Int> VisibleTiles(bool[,] terrain, Vector3Int origin, int radius)
    {
        var result = new HashSet<Vector3Int>();
        if (!GridMovement.IsWalkable(terrain, origin)) return result;
        for (int x = Mathf.Max(0, origin.x - radius); x <= Mathf.Min(terrain.GetLength(0) - 1, origin.x + radius); x++)
        for (int y = Mathf.Max(0, origin.y - radius); y <= Mathf.Min(terrain.GetLength(1) - 1, origin.y + radius); y++)
        {
            var target = new Vector3Int(x, y, 0);
            if (HasLineOfSight(terrain, origin, target)) result.Add(target);
        }
        return result;
    }

    internal static bool HasLineOfSight(bool[,] terrain, Vector3Int from, Vector3Int to)
    {
        if (terrain == null || !GridMovement.IsWalkable(terrain, from) ||
            !GridMovement.Contains(terrain.GetLength(0), terrain.GetLength(1), to)) return false;
        int dx = Mathf.Abs(to.x - from.x), dy = Mathf.Abs(to.y - from.y);
        int sx = System.Math.Sign(to.x - from.x), sy = System.Math.Sign(to.y - from.y);
        int ix = 0, iy = 0;
        var cell = from;
        while (ix < dx || iy < dy)
        {
            int decision = (1 + 2 * ix) * dy - (1 + 2 * iy) * dx;
            if (decision == 0)
            {
                // Sight cannot squeeze diagonally past either blocked side of a corner.
                if (!GridMovement.IsWalkable(terrain, cell + new Vector3Int(sx, 0, 0)) ||
                    !GridMovement.IsWalkable(terrain, cell + new Vector3Int(0, sy, 0))) return false;
                cell += new Vector3Int(sx, sy, 0);
                ix++; iy++;
            }
            else if (decision < 0) { cell.x += sx; ix++; }
            else { cell.y += sy; iy++; }
            if (cell == to) return true;
            if (!GridMovement.IsWalkable(terrain, cell)) return false;
        }
        return true;
    }
}
