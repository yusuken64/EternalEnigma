using System;
using System.Collections.Generic;
using System.Linq;
using TWC;
using UnityEngine;

public class WalkableMap : MonoBehaviour
{
	public TileWorldCreator TileWorldCreator;
	public string WalkableLayerName;

	private bool[,] _walkableMap;

	private void Awake()
	{
		TileWorldCreator.OnBlueprintLayersComplete += blueprintLayersComplete;
	}

	private void OnDestroy()
	{
		TileWorldCreator.OnBlueprintLayersComplete -= blueprintLayersComplete;
	}

	private void blueprintLayersComplete(TileWorldCreator _twc)
	{
		int width = _twc.twcAsset.mapWidth;
		int height = _twc.twcAsset.mapHeight;
		_walkableMap = new bool[width, height];

		bool[,] houseMap = _twc.GetMapOutputFromBlueprintLayer("Houses");
		bool[,] treeMap = _twc.GetMapOutputFromBlueprintLayer("Trees");
		
		for (int x = 0; x < width; x++)
		{
			for (int y = 0; y < height; y++)
			{
				bool hasHouse = houseMap != null && houseMap[x, y];
				bool hasTree = treeMap != null && treeMap[x, y];

				_walkableMap[x, y] = !(hasHouse || hasTree);
			}
		}
	}

	internal Vector3 CellToWorld(Vector3Int newMapPosition)
	{
		//float cellSize = TileWorldCreator.twcAsset.cellSize;
		float cellSize = 2;
		return new Vector3(newMapPosition.x * cellSize,
			newMapPosition.y * cellSize,
			newMapPosition.z * cellSize);
	}

	internal CoordValue<bool> RandomEntrancePosition()
	{
		var floorMap = TileWorldCreator.GetMapOutputFromBlueprintLayer("DungeonPosition");
		var startPos = TileWorldDungeon.Flatten(floorMap, (x) => x).Sample();
		return startPos;
	}

	internal List<CoordValue<bool>> RandomEntrancePositions(int sampleCount)
	{
		var floorMap = TileWorldCreator.GetMapOutputFromBlueprintLayer("DungeonPosition");
		return TileWorldDungeon.Flatten(floorMap, (x) => x).Sample(sampleCount).ToList();
	}

	internal CoordValue<bool> RandomStartPlayerPosition()
	{
		var floorMap = TileWorldCreator.GetMapOutputFromBlueprintLayer("PlayerStartPosition");
		var startPos = TileWorldDungeon.Flatten(floorMap, (x) => x).Sample();
		return startPos;
	}

	internal CoordValue<bool> RandomOpenPosition()
	{
		var floorMap = TileWorldCreator.GetMapOutputFromBlueprintLayer(WalkableLayerName);
		var startPos = TileWorldDungeon.Flatten(floorMap, (x) => x).Sample();
		return startPos;
	}

	internal bool CanWalkTo(Vector3Int from, Vector3Int to)
	{
		int width = _walkableMap.GetLength(0);
		int height = _walkableMap.GetLength(1);

		if (to.x < 0 || to.y < 0 || to.x >= width || to.y >= height)
		{
			return false;
		}

		return _walkableMap[to.x, to.y];
	}
}