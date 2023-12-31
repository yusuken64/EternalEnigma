using System;
using System.Collections.Generic;
using UnityEngine;

public static class BFS
{
    public class Node
    {
        public int X { get; }
        public int Y { get; }
        public bool IsVisited { get; set; }
        public Node Parent { get; set; }

        public Node(int x, int y)
        {
            X = x;
            Y = y;
            IsVisited = false;
            Parent = null;
        }
    }

    public static List<Node> FindPath(Node[,] grid, Node startNode, Func<Node, bool> predicate)
    {
        Queue<Node> queue = new Queue<Node>();
        List<Node> path = new List<Node>();

        queue.Enqueue(startNode);
        startNode.IsVisited = true;

        while (queue.Count > 0)
        {
            Node currentNode = queue.Dequeue();

            if (predicate(currentNode))
            {
                return RetracePath(startNode, currentNode);
            }

            foreach (Node neighbor in GetNeighbors(grid, currentNode))
            {
                if (!neighbor.IsVisited)
                {
                    neighbor.IsVisited = true;
                    neighbor.Parent = currentNode;
                    queue.Enqueue(neighbor);
                }
            }
        }

        return path; // No path found
    }

    static List<Node> RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>();
        Node currentNode = endNode;

        do
        {
            path.Add(currentNode);
            currentNode = currentNode.Parent;
        } while (currentNode != null &&
                 currentNode != startNode);

        path.Reverse();
        return path;
    }

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
                if (grid[neighborX, neighborY] != null)
                {
                    neighbors.Add(grid[neighborX, neighborY]);
                }
            }
        }

        return neighbors;
    }
}
