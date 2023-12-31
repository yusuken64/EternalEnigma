using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MazeGenerator
{
    private int width;
    private int height;
    private bool[,] visited;
    public MazeCell[,] Maze;

    private Dictionary<MazeDirection, Vector3Int> DirectionOffset = new Dictionary<MazeDirection, Vector3Int>()
    {
        { MazeDirection.Up, new Vector3Int(0, 1) },
        { MazeDirection.Right, new Vector3Int(1, 0) },
        { MazeDirection.Down, new Vector3Int(0, -1) },
        { MazeDirection.Left, new Vector3Int(-1, 0) },
    };

    public MazeGenerator(int width, int height)
    {
        this.width = width;
        this.height = height;
        visited = new bool[width, height];
        Maze = new MazeCell[width, height];
    }

    public void GenerateMaze()
    {
        InitializeMaze();
        GeneratePath(new Vector3Int(0, 0));
    }

    private void InitializeMaze()
    {
        // Initialize maze with walls
        for (int i = 0; i < Maze.GetLength(0); i++)
        {
            for (int j = 0; j < Maze.GetLength(1); j++)
            {
                Maze[i, j] = MazeCell.Wall;
            }
        }
    }

    private void GeneratePath(Vector3Int position)
    {
        visited[position.x, position.y] = true;
        Maze[position.x, position.y] = MazeCell.Path;

        MazeDirection[] directions = 
        {
            MazeDirection.Up,
            MazeDirection.Right,
            MazeDirection.Down,
            MazeDirection.Left
        };
        Shuffle(directions);

        foreach (MazeDirection dir in directions)
        {
            var newPosition = position + DirectionOffset[dir];
            var newPosition2 = newPosition + DirectionOffset[dir];

            if (IsInBounds(newPosition2) && !visited[newPosition2.x, newPosition2.y])
            {
                Maze[newPosition.x, newPosition.y] = MazeCell.Path;
                GeneratePath(newPosition2);
            }
        }
    }

    private bool IsInBounds(Vector3Int position)
    {
        return position.x >= 0 && position.x < width && position.y >= 0 && position.y < height;
    }

    private void Shuffle(MazeDirection[] array)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            MazeDirection temp = array[i];
            array[i] = array[j];
            array[j] = temp;
        }
    }
}

public enum MazeCell
{
    Path,
    Wall
}

public enum MazeDirection
{
    Up,
    Right,
    Down,
    Left,
}