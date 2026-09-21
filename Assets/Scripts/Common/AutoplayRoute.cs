using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Follows a route from the game's shared A*, replanning after displacement, obstruction or a new objective.</summary>
public sealed class AutoplayRoute
{
    private readonly Queue<Vector3Int> steps = new();
    private Vector3Int? objective, previousPosition;
    public Vector3Int[] Remaining => steps.ToArray();
    public void Clear() { steps.Clear(); objective = previousPosition = null; }

    public Vector3Int? Next(Vector3Int position, Vector3Int target, Func<AStar.Node[,]> createGrid,
        Func<Vector3Int,Vector3Int,bool> canStep, DiagonalMovement diagonal = DiagonalMovement.RequireOpenSides)
    {
        bool replan = objective != target;
        if (!replan && steps.Count > 0 && position == steps.Peek()) steps.Dequeue();
        else if (previousPosition.HasValue && previousPosition.Value != position) replan = true;
        if (position == target) { Clear(); return null; }
        if (steps.Count == 0 || !canStep(position,steps.Peek())) replan = true;
        if (replan)
        {
            steps.Clear(); objective = target;
            var grid = createGrid();
            if (!GridMovement.Contains(grid.GetLength(0),grid.GetLength(1),position) ||
                !GridMovement.Contains(grid.GetLength(0),grid.GetLength(1),target)) return null;
            var path = AStar.FindPath(grid,grid[position.x,position.y],grid[target.x,target.y],diagonal);
            if (path != null) foreach (var node in path) steps.Enqueue(new Vector3Int(node.X,node.Y));
        }
        previousPosition = position;
        return steps.Count > 0 && canStep(position,steps.Peek()) ? steps.Peek() : null;
    }
}
