using System;
using System.Collections.Generic;
using UnityEngine;

public enum DiagonalMovement
{
    AllowCornerCutting,
    RequireOpenSides
}

// Terrain rules only. Occupancy, turns, interactions and input belong to each mode.
public static class GridMovement
{
    private static readonly Facing[] Directions = {
        Facing.DownLeft, Facing.Down, Facing.DownRight, Facing.Left,
        Facing.Right, Facing.UpLeft, Facing.Up, Facing.UpRight
    };

    public static bool Contains(int width, int height, Vector3Int cell) =>
        cell.z == 0 && cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;

    public static bool IsWalkable(bool[,] terrain, Vector3Int cell) =>
        terrain != null && Contains(terrain.GetLength(0), terrain.GetLength(1), cell) && terrain[cell.x, cell.y];

    public static bool CanStep(Vector3Int from, Vector3Int to, Func<Vector3Int, bool> isWalkable,
        DiagonalMovement diagonal = DiagonalMovement.RequireOpenSides)
    {
        var delta = to - from;
        if (from.z != 0 || to.z != 0 || delta == Vector3Int.zero ||
            Mathf.Abs(delta.x) > 1 || Mathf.Abs(delta.y) > 1 || !isWalkable(to)) return false;

        return diagonal == DiagonalMovement.AllowCornerCutting || delta.x == 0 || delta.y == 0 ||
            (isWalkable(from + new Vector3Int(delta.x, 0, 0)) &&
             isWalkable(from + new Vector3Int(0, delta.y, 0)));
    }

    public static IEnumerable<Facing> GetValidDirections(Vector3Int from, Func<Vector3Int, bool> isWalkable,
        DiagonalMovement diagonal = DiagonalMovement.RequireOpenSides)
    {
        foreach (var facing in Directions)
            if (CanStep(from, from + GetFacingOffset(facing), isWalkable, diagonal)) yield return facing;
    }

    public static IEnumerable<Vector3Int> GetNeighbors(Vector3Int from, Func<Vector3Int, bool> isWalkable,
        DiagonalMovement diagonal = DiagonalMovement.RequireOpenSides)
    {
        foreach (var facing in GetValidDirections(from, isWalkable, diagonal))
            yield return from + GetFacingOffset(facing);
    }

    public static Vector3 CellToWorld(Vector3Int cell, float cellSize) => (Vector3)cell * cellSize;

    public static Vector3Int GetFacingOffset(Facing facing)
    {
        switch (facing)
        {
            case Facing.Up: return new Vector3Int(0, 1, 0);
            case Facing.Down: return new Vector3Int(0, -1, 0);
            case Facing.Left: return new Vector3Int(-1, 0, 0);
            case Facing.Right: return new Vector3Int(1, 0, 0);
            case Facing.UpLeft: return new Vector3Int(-1, 1, 0);
            case Facing.UpRight: return new Vector3Int(1, 1, 0);
            case Facing.DownLeft: return new Vector3Int(-1, -1, 0);
            case Facing.DownRight: return new Vector3Int(1, -1, 0);
            default: return Vector3Int.zero;
        }
    }
}
