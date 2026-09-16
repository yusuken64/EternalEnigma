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
		public int MovePenalty { get; }
		public double GCost { get; set; } // (Actual Cost)
        public double HCost { get; set; } // (Heuristic Cost)
        public double FCost => GCost + HCost;
        public Node Parent { get; set; }

        public Node(int x, int y, bool isWalkable, int movePenalty)
        {
            X = x;
            Y = y;
            IsWalkable = isWalkable;
			MovePenalty = movePenalty;
		}
    }

    public static List<Node> FindPath(Node[,] grid, Node startNode, Node targetNode,
        DiagonalMovement diagonal = DiagonalMovement.RequireOpenSides)
    {
        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();

        if (startNode == null || targetNode == null) return null;
        foreach (var node in grid)
        {
            if (node == null) continue;
            node.GCost = double.PositiveInfinity;
            node.HCost = 0;
            node.Parent = null;
        }
        startNode.GCost = 0;
        openSet.Add(startNode);

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
			closedSet.Add(currentNode);

			if (targetNode == currentNode)
			{
				return RetracePath(startNode, targetNode);
			}

			foreach (Node neighbor in GetNeighbors(grid, currentNode, diagonal))
			{
				if (!neighbor.IsWalkable || closedSet.Contains(neighbor))
				{
					continue;
				}

				double newMovementCostToNeighbor = currentNode.GCost + GetDistance(currentNode, neighbor) + neighbor.MovePenalty;
				if (newMovementCostToNeighbor < neighbor.GCost || !openSet.Contains(neighbor))
				{
					neighbor.GCost = newMovementCostToNeighbor;
					neighbor.HCost = GetDistance(neighbor, targetNode);
					neighbor.Parent = currentNode;

					if (!openSet.Contains(neighbor))
					{
						openSet.Add(neighbor);
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

    static IEnumerable<Node> GetNeighbors(Node[,] grid, Node node, DiagonalMovement diagonal)
    {
        bool IsWalkable(Vector3Int cell) =>
            GridMovement.Contains(grid.GetLength(0), grid.GetLength(1), cell) &&
            grid[cell.x, cell.y] != null && grid[cell.x, cell.y].IsWalkable;
        foreach (var cell in GridMovement.GetNeighbors(new Vector3Int(node.X, node.Y), IsWalkable, diagonal))
            yield return grid[cell.x, cell.y];
    }

    static double GetDistance(Node nodeA, Node nodeB)
    {
        int distanceX = Mathf.Abs(nodeA.X - nodeB.X);
        int distanceY = Mathf.Abs(nodeA.Y - nodeB.Y);

        return Mathf.Sqrt(distanceX * distanceX + distanceY * distanceY);
    }
}
