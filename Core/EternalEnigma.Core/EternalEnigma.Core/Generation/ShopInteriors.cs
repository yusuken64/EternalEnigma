using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

/// <summary>
/// Carves interior shop rooms from door markers. One room per true `doors` cell whose raster index
/// (x-outer, y-inner) maps to a true shopFlags entry.
/// </summary>
public static class ShopInteriors
{
    /// <summary>
    /// Computes shop interior rooms from door markers, allies, and shop flags.
    /// One room per true `doors` cell whose raster index (x-outer, y-inner) maps to a true shopFlags entry.
    /// Skips rooms that go out of bounds, overlap an already claimed cell, or touch an ally cell.
    /// </summary>
    public static List<ShopRoom> ComputeRooms(GridLayer? doors, GridLayer? allies, IReadOnlyList<bool>? shopFlags, int width, int height)
    {
        var rooms = new List<ShopRoom>();
        if (doors == null || shopFlags == null) return rooms;

        var claimed = new HashSet<GridPoint>();
        int shopIndex = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!doors.At(new GridPoint(x, y))) continue;

                bool isShop = shopIndex < shopFlags.Count && shopFlags[shopIndex];
                shopIndex++;
                if (!isShop) continue;

                var door = new GridPoint(x, y);
                var floor = new List<GridPoint>
                {
                    new(x, y + 1),
                    new(x, y + 2),
                    new(x, y + 3)
                };

                var wall = new List<GridPoint>();
                for (int dy = 1; dy <= 4; dy++)
                {
                    foreach (int dx in dy == 4 ? new[] { -1, 0, 1 } : new[] { -1, 1 })
                    {
                        wall.Add(new GridPoint(x + dx, y + dy));
                    }
                }

                // Check if all floor and wall cells fit
                bool fits = true;
                var allCells = floor.Concat(wall);
                foreach (var cell in allCells)
                {
                    if (cell.X < 0 || cell.Y < 0 || cell.X >= width || cell.Y >= height ||
                        claimed.Contains(cell) || (allies != null && allies.At(cell)))
                    {
                        fits = false;
                        break;
                    }
                }

                if (!fits) continue;

                // Claim all cells and add the room
                foreach (var c in floor) claimed.Add(c);
                foreach (var c in wall) claimed.Add(c);
                rooms.Add(new ShopRoom(door, floor, wall));
            }
        }

        return rooms;
    }

    /// <summary>
    /// The floor cell against the room's back wall, where the vendor stands facing the door.
    /// </summary>
    public static GridPoint InteriorAnchor(GridPoint door) => new(door.X, door.Y + 3);

    /// <summary>
    /// Checks if an interior anchor exists for the given door. The anchor is the location
    /// at InteriorAnchor(door), and this returns true only if the floor layer exists and
    /// contains that anchor.
    /// </summary>
    public static bool TryGetInteriorAnchor(GridLayer? floor, GridPoint door, out GridPoint anchor)
    {
        anchor = InteriorAnchor(door);
        return floor != null && floor.At(anchor);
    }
}
