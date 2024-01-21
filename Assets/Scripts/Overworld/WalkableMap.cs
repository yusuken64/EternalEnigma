using System;
using System.Collections.Generic;
using System.Linq;
using TWC;
using UnityEngine;

public class WalkableMap : MonoBehaviour
{
	public TileWorldCreator TileWorldCreator;
	public string WalkableLayerName;

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
		var walkableMap = TileWorldCreator.GetMapOutputFromBlueprintLayer(WalkableLayerName);
		if (to.x < 0 || to.x >= walkableMap.GetLength(0)||
			to.y < 0 || to.y >= walkableMap.GetLength(1))
		{
			return false;
		}

		return walkableMap[to.x, to.y];
	}
}