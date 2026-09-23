using System.Collections.Generic;

namespace EternalEnigma.Core.World;

/// <summary>Deterministic tile sight. Walls are visible endpoints but block everything behind them.</summary>
public static class GridSight
{
    private static bool Walkable(GridLayer t, int x, int y) =>
        x >= 0 && y >= 0 && x < t.Width && y < t.Height && t[x, y];

    public static bool IsRoom(GridLayer terrain, GridPoint origin)
    {
        // A room tile belongs to an open 2x2 area. One-tile passages remain corridors.
        for (int dx = -1; dx <= 0; dx++)
        for (int dy = -1; dy <= 0; dy++)
        {
            int cornerX = origin.X + dx;
            int cornerY = origin.Y + dy;
            if (Walkable(terrain, cornerX, cornerY) &&
                Walkable(terrain, cornerX + 1, cornerY) &&
                Walkable(terrain, cornerX, cornerY + 1) &&
                Walkable(terrain, cornerX + 1, cornerY + 1)) return true;
        }
        return false;
    }

    public static HashSet<GridPoint> VisibleTiles(GridLayer terrain, GridPoint origin, int radius)
    {
        var result = new HashSet<GridPoint>();
        if (!Walkable(terrain, origin.X, origin.Y)) return result;
        for (int x = Math.Max(0, origin.X - radius); x <= Math.Min(terrain.Width - 1, origin.X + radius); x++)
        for (int y = Math.Max(0, origin.Y - radius); y <= Math.Min(terrain.Height - 1, origin.Y + radius); y++)
        {
            var target = new GridPoint(x, y);
            if (HasLineOfSight(terrain, origin, target)) result.Add(target);
        }
        return result;
    }

    public static bool HasLineOfSight(GridLayer terrain, GridPoint from, GridPoint to)
    {
        if (!Walkable(terrain, from.X, from.Y) || to.X < 0 || to.Y < 0 || to.X >= terrain.Width || to.Y >= terrain.Height)
            return false;

        int dx = Math.Abs(to.X - from.X);
        int dy = Math.Abs(to.Y - from.Y);
        int sx = Math.Sign(to.X - from.X);
        int sy = Math.Sign(to.Y - from.Y);
        int ix = 0, iy = 0;
        int cellX = from.X, cellY = from.Y;

        while (ix < dx || iy < dy)
        {
            int decision = (1 + 2 * ix) * dy - (1 + 2 * iy) * dx;
            if (decision == 0)
            {
                // Sight cannot squeeze diagonally past either blocked side of a corner.
                if (!Walkable(terrain, cellX + sx, cellY) || !Walkable(terrain, cellX, cellY + sy))
                    return false;
                cellX += sx;
                cellY += sy;
                ix++;
                iy++;
            }
            else if (decision < 0)
            {
                cellX += sx;
                ix++;
            }
            else
            {
                cellY += sy;
                iy++;
            }

            if (cellX == to.X && cellY == to.Y) return true;
            if (!Walkable(terrain, cellX, cellY)) return false;
        }
        return true;
    }
}
