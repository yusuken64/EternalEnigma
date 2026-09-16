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

    public static List<Node> FindPath(Node[,] grid, Node startNode, Func<Node, bool> predicate,
        DiagonalMovement diagonal = DiagonalMovement.RequireOpenSides)
    {
        Queue<Node> queue = new Queue<Node>();
        List<Node> path = new List<Node>();

        if (startNode == null) return path;
        foreach (var node in grid)
        {
            if (node == null) continue;
            node.IsVisited = false;
            node.Parent = null;
        }
        queue.Enqueue(startNode);
        startNode.IsVisited = true;

        while (queue.Count > 0)
        {
            Node currentNode = queue.Dequeue();

            if (predicate(currentNode))
            {
                return RetracePath(startNode, currentNode);
            }

            foreach (Node neighbor in GetNeighbors(grid, currentNode, diagonal))
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

    static IEnumerable<Node> GetNeighbors(Node[,] grid, Node node, DiagonalMovement diagonal)
    {
        bool IsWalkable(Vector3Int cell) =>
            GridMovement.Contains(grid.GetLength(0), grid.GetLength(1), cell) && grid[cell.x, cell.y] != null;
        foreach (var cell in GridMovement.GetNeighbors(new Vector3Int(node.X, node.Y), IsWalkable, diagonal))
            yield return grid[cell.x, cell.y];
    }
}
