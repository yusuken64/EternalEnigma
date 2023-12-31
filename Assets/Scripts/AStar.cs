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

    public static List<Node> FindPath(Node[,] grid, Node startNode, Node targetNode)
    {
        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();

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

			foreach (Node neighbor in GetNeighbors(grid, currentNode))
			{
				if (!neighbor.IsWalkable || closedSet.Contains(neighbor))
				{
					continue;
				}

				double newMovementCostToNeighbor = currentNode.GCost + GetDistance(currentNode, neighbor);
				if (newMovementCostToNeighbor < neighbor.GCost || !openSet.Contains(neighbor))
				{
					neighbor.GCost = newMovementCostToNeighbor + neighbor.MovePenalty;
					neighbor.HCost = GetDistance(neighbor, targetNode) + neighbor.MovePenalty;
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

    static Vector3Int from;
    static Vector3Int to;
    static List<Node> GetNeighbors(Node[,] grid, Node node)
    {
        List<Node> neighbors = new List<Node>();
        int[] xOffset = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] yOffset = { -1, -1, -1, 0, 0, 1, 1, 1 };

        for (int i = 0; i < 8; i++)
        {
            int neighborX = node.X + xOffset[i];
            int neighborY = node.Y + yOffset[i];

            if (neighborX >= 0 && neighborX < grid.GetLength(0) && neighborY >= 0 && neighborY < grid.GetLength(1))
            {
                from.x = node.X;
                from.y = node.Y;
                to.x = neighborX;
                to.y = neighborY;
                if (Game.Instance.CurrentDungeon.CanWalkTo(from, to))
                {
                    neighbors.Add(grid[neighborX, neighborY]);
                }
            }
        }

        return neighbors;
    }

    static double GetDistance(Node nodeA, Node nodeB)
    {
        int distanceX = Mathf.Abs(nodeA.X - nodeB.X);
        int distanceY = Mathf.Abs(nodeA.Y - nodeB.Y);

        return Mathf.Sqrt(distanceX * distanceX + distanceY * distanceY);
    }
}
