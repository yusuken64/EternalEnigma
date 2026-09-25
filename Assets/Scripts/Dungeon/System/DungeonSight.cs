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
}
