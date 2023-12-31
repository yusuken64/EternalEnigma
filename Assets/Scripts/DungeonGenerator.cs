using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DungeonGenerator : MonoBehaviour
{
	public Dungeon DungeonPrefab;

	public int dungeonWidth = 100;
	public int dungeonHeight = 100;
	public int minRoomSize = 5; // Minimum size of each leaf node

	public float alpha = 0.5f;
	public float beta = 0.75f;

	public Tile StairTile;
	public Tile StairEndTile;
	public TreasureTile TreasureTile;
	public DroppedItemTile DroppedItemTile;

	private Vector3Int startPosition;

	public RuleTile BaseRuleTile;
	public RuleTile DungeonRuleTile;

	public List<Tile> BaseRegions;
	public float PerlinXMultiplier;
	public float PerlinYMultiplier;
	public int PerlinXOffset;
	public int PerlinYOffset;

	public Dictionary<Vector3Int, GameTileData> TileGameDataLookup;

	public float PillarChance;
	public float MazeChance;


	internal Vector3Int GetStartPositioon()
	{
		return startPosition;
	}

	internal Dungeon GenerateDungeon()
	{
		TileGameDataLookup = new();
		var dungeon = Instantiate(DungeonPrefab, Game.Instance.transform);
		dungeon.InitializeGrid(dungeonWidth, dungeonHeight);

		DrawBase(dungeon);
		var bspRoot = GenerateBSP();
		dungeon.BSPRoot = bspRoot;

		GenerateRooms(bspRoot);
		DrawRooms(bspRoot, dungeon);
		GenerateRoads(bspRoot);
		DrawRoads(bspRoot, dungeon);
		GenerateStairs(bspRoot, dungeon);

		return dungeon;
	}

	//[ContextMenu("Generate")]
	//public void Generate()
	//{
	//	Debug.Log("Generating Dungeon");

	//	GenerateBSP();

	//	Debug.Log("Dungeon Generated");
	//}

	//[ContextMenu("GenerateRooms")]
	//public void GenerateRoomsCommand()
	//{
	//	Debug.Log("Generating Rooms");
	//	GenerateRooms(BSPRoot);
	//	DrawRooms(BSPRoot);

	//	Debug.Log("Rooms Generated");
	//}


	//[ContextMenu("GenerateRoads")]
	//public void GenerateRoadsCommand()
	//{
	//	Debug.Log("Generating Roads");
	//	GenerateRoads(BSPRoot);
	//	DrawRoads(BSPRoot);

	//	Debug.Log("Roads Generated");
	//}

	[ContextMenu("GenerateAll")]
	public void GenerateAllCommand()
	{
		TileGameDataLookup = new Dictionary<Vector3Int, GameTileData>();
	}

	private void DrawBase(Dungeon dungeon)
	{
		var perlinXOffset = PerlinXOffset + UnityEngine.Random.Range(0, 100);
		var perlinYOffset = PerlinYOffset + UnityEngine.Random.Range(0, 100);
		for (int i = 0; i < dungeonWidth; i++)
		{
			for (int j = 0; j < dungeonHeight; j++)
			{
				Vector3Int position = new Vector3Int(i, j);
				//TileMap.SetTile(position, BaseRuleTile);

				float xCoord = (float)i / dungeonWidth * PerlinXMultiplier;
				float yCoord = (float)j / dungeonHeight * PerlinYMultiplier;
				var perlinNoise = Mathf.PerlinNoise(xCoord + perlinXOffset, yCoord + perlinYOffset);
				var index = Mathf.Clamp((int)(Mathf.Clamp01(perlinNoise) * BaseRegions.Count()), 0, BaseRegions.Count() - 1);
				var tile = BaseRegions[index];
				dungeon.TileMap.SetTile(position, tile);
			}
		}
	}

	private void GenerateStairs(BSPNode bspRoot, Dungeon dungeon)
	{
		startPosition = dungeon.GetRandomEnemyPosition();
		dungeon.ObjectTileMap.SetTile(startPosition, StairTile);

		var stairPosition = dungeon.GetRandomEnemyPosition();
		dungeon.ObjectTileMap.SetTile(stairPosition, StairEndTile);
	}

	private void DrawRoads(BSPNode root, Dungeon dungeon)
	{
		if (root == null) { return; }

		foreach(var roadPosition in root.RoadPositions)
		{
			Vector3Int position = new Vector3Int(roadPosition.X, roadPosition.Y);
			dungeon.TileMap_Floor.SetTile(position, DungeonRuleTile);
		}

		DrawRoads(root.leftChild, dungeon);
		DrawRoads(root.rightChild, dungeon);
	}

	List<(double, BSPNode, BSPNode)> GetNodeDistancecs(List<BSPNode> set1, List<BSPNode> set2)
	{
		List<(double, BSPNode, BSPNode)> distaceNodes = new();

		foreach (var node1 in set1)
		{
			foreach (var node2 in set2)
			{
				double distance = CalculateDistance(node1, node2);
				distaceNodes.Add((distance, node1, node2));
			}
		}

		return distaceNodes;
	}

	private void GenerateRoads(BSPNode root)
	{
		if (root == null) { return; }

		if (root.leftChild?.room != null &&
			root.rightChild?.room != null)
		{
			bool splitHorizontal = root.splitHorizontal;
			BSPNode leftChild = root.leftChild;
			BSPNode rightChild = root.rightChild;

			ConnectRoom(root, leftChild, rightChild, splitHorizontal);
		}
		else
		{
			var set1 = BSPNode.Flatten(root.leftChild)
				.Where(x => x != null)
				.Where(x => x.room != null)
				.Where(x => x != root).ToList();
			var set2 = BSPNode.Flatten(root.rightChild)
				.Where(x => x != null)
				.Where(x => x.room != null)
				.Where(x => x != root).ToList();
			if (set1.Any() && set2.Any())
			{
				var nodeDistances = GetNodeDistancecs(set1, set2)
					.OrderBy(x => x.Item1)
					.ToList();
				if (nodeDistances.Count > 0)
				{
					if (nodeDistances[0].Item2 != null &&
						nodeDistances[0].Item3 != null)
					{
						ConnectRoom(root, nodeDistances[0].Item2, nodeDistances[0].Item3, root.splitHorizontal);
					}
				}

				//For each pair of rooms, compute the distance between them(on the tree)
				//Connect rooms with probability alpha + betadistance - 1
				foreach (var nodeDistance in nodeDistances.Skip(1))
				{
					var chance = alpha + Mathf.Pow(beta, (float)nodeDistance.Item1);
					//Debug.Log($"Chance {chance}");
					if (chance > UnityEngine.Random.value)
					{
						ConnectRoom(root, nodeDistance.Item2, nodeDistance.Item3, root.splitHorizontal);
						break;
					}
				}
			}
		}

		GenerateRoads(root.leftChild);
		GenerateRoads(root.rightChild);
	}

	static BSPNode FindClosestRoom(List<BSPNode> childRooms, BSPNode root)
	{
		double minDistance = double.MaxValue;
		BSPNode closestRoom = null;

		foreach (var child in childRooms)
		{
			double distance = CalculateDistance(child, root);
			if (distance < minDistance)
			{
				minDistance = distance;
				closestRoom = child;
			}
		}

		return closestRoom;
	}

	static double CalculateDistance(BSPNode node1, BSPNode node2)
	{
		var pos1 = node1.RoomCenterPos();
		var pos2 = node2.RoomCenterPos();

		int deltaX = pos2.X - pos1.X;
		int deltaY = pos2.Y - pos1.Y;

		return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
	}

	private static void ConnectRoom(BSPNode root, BSPNode leftChild, BSPNode rightChild, bool splitHorizontal)
	{
		leftChild.Connections.Add(rightChild);
		rightChild.Connections.Add(leftChild);
		if (splitHorizontal)
		{
			var doorPosX1 = UnityEngine.Random.Range(leftChild.room.X + 2, leftChild.room.X + leftChild.room.Width);
			var doorPosX2 = UnityEngine.Random.Range(rightChild.room.X + 2, rightChild.room.X + rightChild.room.Width);

			if ((doorPosX1 - leftChild.room.X) % 2 == 1)
			{
				doorPosX1--;
			}

			if ((doorPosX2 - rightChild.room.X) % 2 == 1)
			{
				doorPosX2--;
			}

			//add road from left door to border
			{
				int starY = leftChild.room.Y + leftChild.room.Height;
				int endY = rightChild.space.Y + 1;
				for (int j = starY; j < endY; j++)
				{
					root.RoadPositions.Add(new BSPPosition(doorPosX1, j));
				}
			}

			//add road from right door to border
			{
				int starY = rightChild.space.Y;
				int endY = rightChild.room.Y;
				for (int j = starY; j < endY; j++)
				{
					root.RoadPositions.Add(new BSPPosition(doorPosX2, j));
				}
			}

			//bridge vertical
			{
				int y = rightChild.space.Y;
				var startX = Mathf.Min(doorPosX1, doorPosX2);
				var endX = Mathf.Max(doorPosX1, doorPosX2);
				for (int i = startX; i < endX; i++)
				{
					root.RoadPositions.Add(new BSPPosition(i, y));
				}
			}
		}
		else
		{
			var doorPosY1 = UnityEngine.Random.Range(leftChild.room.Y + 2, leftChild.room.Y + leftChild.room.Height);
			var doorPosY2 = UnityEngine.Random.Range(rightChild.room.Y + 2, rightChild.room.Y + rightChild.room.Height);

			if ((doorPosY1 - leftChild.room.Y) % 2 == 1)
			{
				doorPosY1--;
			}

			if ((doorPosY2 - rightChild.room.Y) % 2 == 1)
			{
				doorPosY2--;
			}

			//add road from left door to border
			{
				int starX = leftChild.room.X + leftChild.room.Width;
				int endX = rightChild.space.X + 1;
				for (int i = starX; i < endX; i++)
				{
					root.RoadPositions.Add(new BSPPosition(i, doorPosY1));
				}
			}
			//add road from right door to border
			{
				int starX = rightChild.space.X;
				int endX = rightChild.room.X;
				for (int i = starX; i < endX; i++)
				{
					root.RoadPositions.Add(new BSPPosition(i, doorPosY2));
				}
			}

			//bridge vertical
			{
				int x = rightChild.space.X;
				var startY = Mathf.Min(doorPosY1, doorPosY2);
				var endY = Mathf.Max(doorPosY1, doorPosY2);
				for (int j = startY; j < endY; j++)
				{
					root.RoadPositions.Add(new BSPPosition(x, j));
				}
			}
		}
	}

	private void GenerateRooms(BSPNode root)
	{
		if (root == null) { return; }

		if (root.leftChild == null &&
			root.rightChild == null)
		{
			var width = UnityEngine.Random.Range(minRoomSize, root.space.Width) - 3;
			var height = UnityEngine.Random.Range(minRoomSize, root.space.Height) - 3;

			if (width % 2 == 0)
			{
				width--;
			}

			if (height % 2 == 0)
			{
				height--;
			}

			var x = root.space.X + 2 + UnityEngine.Random.Range(0, root.space.Width - width - 3);
			var y = root.space.Y + 2 + UnityEngine.Random.Range(0, root.space.Height - height - 3);
			root.room = new BSPRect(x, y, width, height);

			//Debug.Log($"Created room for {root.label} {root.room.X},{root.room.Y} {root.room.Width}x{root.room.Height}");
		}

		GenerateRooms(root.leftChild);
		GenerateRooms(root.rightChild);
	}

	private void DrawRooms(BSPNode root, Dungeon dungeon)
	{
		if (root == null) { return; }

		if (root.room != null)
		{
			var center = root.RoomCenterPos();
			Vector3Int centerPosition = new Vector3Int(center.X, center.Y);

			for (int i = root.room.X; i < root.room.Width + root.room.X; i++)
			{
				for (int j = root.room.Y; j < root.room.Height + root.room.Y; j++)
				{
					Vector3Int position = new Vector3Int(i, j);
					dungeon.TileMap_Floor.SetTile(position, DungeonRuleTile);

					TileGameDataLookup[position] = new GameTileData()
					{
						node = root,
					};
				}
			}

			var pillars = UnityEngine.Random.value < PillarChance;
			if (pillars)
			{
				int x = 0;
				int y = 0;

				for (int i = root.room.X; i < root.room.Width + root.room.X; i++)
				{
					x++;
					y = 0;
					for (int j = root.room.Y; j < root.room.Height + root.room.Y; j++)
					{
						y++;
						if (x % 2 == 0 &&
							y % 2 == 0)
						{
							Vector3Int position = new Vector3Int(i, j);
							dungeon.TileMap_Floor.SetTile(position, null);
						}
					}
				}
			}

			var maze = UnityEngine.Random.value < MazeChance;
			if (maze)
			{
				MazeGenerator mazeGenerator = new MazeGenerator(root.room.Width, root.room.Height);
				mazeGenerator.GenerateMaze();
				var generatedMaze = mazeGenerator.Maze;

				int x = 0;
				int y = 0;
				for (int i = root.room.X; i < root.room.Width + root.room.X; i++)
				{
					y = 0;
					for (int j = root.room.Y; j < root.room.Height + root.room.Y; j++)
					{
						if (generatedMaze[x, y] == MazeCell.Wall)
						{
							Vector3Int position = new Vector3Int(i, j);
							dungeon.TileMap_Floor.SetTile(position, null);
						}
						y++;
					}
					x++;
				}
			}
		}

		DrawRooms(root.leftChild, dungeon);
		DrawRooms(root.rightChild, dungeon);
	}

	BSPNode GenerateBSP()
	{
		var bspRoot = new BSPNode()
		{
			space = new BSPRect(0, 0, dungeonWidth, dungeonHeight),
			label = "R"
		};

		BSP(bspRoot);
		return bspRoot;
	}

	private void DrawSpaces(BSPNode root, Dungeon dungeon)
	{
		if (root == null) { return; }

		for (int i = root.space.X; i < root.space.Width + root.space.X; i++)
		{
			for (int j = root.space.Y; j < root.space.Height + root.space.Y; j++)
			{
				if (i == root.space.X || i == root.space.Width + root.space.X - 1 ||
					j == root.space.Y || j == root.space.Height + root.space.Y - 1)
				{
					Vector3Int position = new Vector3Int(i, j);
					dungeon.TileMap.SetTile(position, BaseRuleTile);
				}
			}
		}

		DrawSpaces(root.leftChild, dungeon);
		DrawSpaces(root.rightChild, dungeon);
	}

	private void BSP(BSPNode node)
	{
		if (node == null) { return; }

		var canSplitHorizontal = node.space.Width > minRoomSize;
		var canSplitVertical = node.space.Height > minRoomSize;

		bool splitHorizontal;
		if (canSplitHorizontal && canSplitVertical)
		{
			splitHorizontal = UnityEngine.Random.value > 0.5f;
		} else if (canSplitHorizontal)
		{
			splitHorizontal = true;
		}
		else
		{
			splitHorizontal = false;
		}
		node.splitHorizontal = splitHorizontal;

		if (splitHorizontal)
		{
			if (node.space.Height > minRoomSize)
			{
				int value = UnityEngine.Random.Range(minRoomSize, node.space.Height);
				var min = Math.Min(minRoomSize, value);
				var max = Math.Max(minRoomSize, value);
				var height1 = Math.Clamp(value, min, max);
				var height2 = node.space.Height - height1 + 1;

				if (height1 > minRoomSize &&
					height2 > minRoomSize) 
				{
					node.leftChild = new BSPNode()
					{
						space = new BSPRect(node.space.X, node.space.Y, node.space.Width, height1),
						label = node.label + "A"
					};

					node.rightChild = new BSPNode()
					{
						space = new BSPRect(node.space.X, node.space.Y + height1 - 1, node.space.Width, height2),
						label = node.label + "B"
					};
				}
			}
		}
		else
		{
			if (node.space.Width > minRoomSize)
			{
				var value = UnityEngine.Random.Range(minRoomSize, node.space.Width);
				var min = Math.Min(minRoomSize, value);
				var max = Math.Max(minRoomSize, value);
				var width1 = Math.Clamp(value, min, max);
				var width2 = node.space.Width - width1 + 1;

				if (width1 > minRoomSize &&
					width2 > minRoomSize)
				{
					node.leftChild = new BSPNode()
					{
						space = new BSPRect(node.space.X, node.space.Y, width1, node.space.Height),
						label = node.label + "A"
					};

					node.rightChild = new BSPNode()
					{
						space = new BSPRect(node.space.X + width1 - 1, node.space.Y, width2, node.space.Height),
						label = node.label + "B"
					};
				}
			}
		}

		BSP(node.leftChild);
		BSP(node.rightChild);
	}

}
public class BSPNode
{
	public string label;
	public BSPRect space;
	public BSPRect room;
	public BSPNode leftChild;
	public BSPNode rightChild;
	internal bool splitHorizontal;

	public List<BSPPosition> RoadPositions = new();

	public List<BSPNode> Connections = new();

	internal BSPPosition RoomCenterPos()
	{
		return new BSPPosition(room.X + (room.Width / 2),
			room.Y + (room.Height / 2));
	}

	public static List<BSPNode> Flatten(BSPNode node)
	{
		if (node == null)
		{
			return new();
		}

		List<BSPNode> result = new();
		result.AddRange(Flatten(node.leftChild));
		result.Add(node);
		result.AddRange(Flatten(node.rightChild));

		return result;
	}
}

public class BSPPosition
{
	public int X;
	public int Y;

	public BSPPosition(int x, int y)
	{
		X = x;
		Y = y;
	}
}

public class BSPRect
{
    public int X;
    public int Y;
    public int Width;
    public int Height;

	public BSPRect(int x, int y, int width, int height)
	{
		this.X = x;
		this.Y = y;
		this.Width = width;
		this.Height = height;
	}
}