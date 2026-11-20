using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class AStar
{
    public class Node
    {
        public int X { get; }
        public int Y { get; }
        public bool IsWalkable { get; set; }
        public bool CardinalOnly { get; set; }
		public int MovePenalty { get; }
		public double GCost { get; set; } // (Actual Cost)
        public double HCost { get; set; } // (Heuristic Cost)
        public double FCost => GCost + HCost;
        public Node Parent { get; set; }
        // Search bookkeeping, reset at the start of every FindPath (replaces a per-call HashSet and List.Contains).
        internal bool InOpen;
        internal bool Closed;

        public Node(int x, int y, bool isWalkable, int movePenalty)
        {
            X = x;
            Y = y;
            IsWalkable = isWalkable;
			MovePenalty = movePenalty;
		}
    }

    // Main-thread only and non-reentrant: step predicates must not start another search.
    static readonly List<Node> SharedOpenSet = new List<Node>(64);
    // Same order as GridMovement's Directions: DownLeft, Down, DownRight, Left, Right, UpLeft, Up, UpRight.
    static readonly int[] NeighborDx = { -1, 0, 1, -1, 1, -1, 0, 1 };
    static readonly int[] NeighborDy = { -1, -1, -1, 0, 0, 1, 1, 1 };

    public static List<Node> FindPath(Node[,] grid, Node startNode, Node targetNode,
        DiagonalMovement diagonal = DiagonalMovement.RequireOpenSides,
        System.Func<Vector3Int, Vector3Int, bool> canStep = null)
    {
        if (startNode == null || targetNode == null) return null;
        List<Node> openSet = SharedOpenSet;
        openSet.Clear();
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        foreach (var node in grid)
        {
            if (node == null) continue;
            node.GCost = double.PositiveInfinity;
            node.HCost = 0;
            node.Parent = null;
            node.InOpen = false;
            node.Closed = false;
        }
        startNode.GCost = 0;
        openSet.Add(startNode);
        startNode.InOpen = true;

        while (openSet.Count > 0)
		{
			Node currentNode = openSet[0];
			for (int i = 1; i < openSet.Count; i++)
			{
				if (openSet[i].FCost < currentNode.FCost || openSet[i].FCost == currentNode.FCost && openSet[i].HCost < currentNode.HCost)
				{
					currentNode = openSet[i];
				}
			}

			openSet.Remove(currentNode);
			currentNode.InOpen = false;
			currentNode.Closed = true;

			if (targetNode == currentNode)
			{
				openSet.Clear();
				return RetracePath(startNode, targetNode);
			}

			// Inlined equivalent of GridMovement.GetNeighbors + CardinalOnly filter (same order, same rules).
			for (int d = 0; d < NeighborDx.Length; d++)
			{
				int dx = NeighborDx[d];
				int dy = NeighborDy[d];
				Node neighbor = GetWalkable(grid, width, height, currentNode.X + dx, currentNode.Y + dy);
				if (neighbor == null) continue;
				if (canStep != null && !canStep(new Vector3Int(currentNode.X, currentNode.Y), new Vector3Int(neighbor.X, neighbor.Y))) continue;
				if (dx != 0 && dy != 0)
				{
					if (canStep == null && diagonal != DiagonalMovement.AllowCornerCutting &&
						(GetWalkable(grid, width, height, currentNode.X + dx, currentNode.Y) == null ||
						 GetWalkable(grid, width, height, currentNode.X, currentNode.Y + dy) == null))
						continue;
					if (currentNode.CardinalOnly || neighbor.CardinalOnly) continue;
				}

				if (neighbor.Closed)
				{
					continue;
				}

				double newMovementCostToNeighbor = currentNode.GCost + GetDistance(currentNode, neighbor) + neighbor.MovePenalty;
				if (newMovementCostToNeighbor < neighbor.GCost || !neighbor.InOpen)
				{
					neighbor.GCost = newMovementCostToNeighbor;
					neighbor.HCost = GetDistance(neighbor, targetNode);
					neighbor.Parent = currentNode;

					if (!neighbor.InOpen)
					{
						openSet.Add(neighbor);
						neighbor.InOpen = true;
					}
				}
			}
		}

		return null;
    }

	static List<Node> RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>();
        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.Parent;
        }

        path.Reverse();
        return path;
    }

    static Node GetWalkable(Node[,] grid, int width, int height, int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return null;
        Node n = grid[x, y];
        return n != null && n.IsWalkable ? n : null;
    }

    static double GetDistance(Node nodeA, Node nodeB)
    {
        int distanceX = Mathf.Abs(nodeA.X - nodeB.X);
        int distanceY = Mathf.Abs(nodeA.Y - nodeB.Y);

        return Mathf.Sqrt(distanceX * distanceX + distanceY * distanceY);
    }
}
