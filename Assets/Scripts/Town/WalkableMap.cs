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
		bool[,] shopWallMap = _twc.GetMapOutputFromBlueprintLayer("ShopWalls");

		for (int x = 0; x < width; x++)
		{
			for (int y = 0; y < height; y++)
			{
				bool hasHouse = houseMap != null && houseMap[x, y];
				bool hasTree = treeMap != null && treeMap[x, y];
				bool hasShopWall = shopWallMap != null && shopWallMap[x, y];

				_walkableMap[x, y] = !(hasHouse || hasTree || hasShopWall);
			}
		}
	}

	internal Vector3 CellToWorld(Vector3Int newMapPosition)
	{
		return GridMovement.CellToWorld(newMapPosition, TileWorldCreator.twcAsset.cellSize);
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

    internal List<Vector3Int> CampaignBuildingPositions(TownConfiguration configuration, IEnumerable<Vector3Int> existing) =>
        CampaignTownCorridor.CompleteBuildingPositions(_walkableMap,
            TileWorldCreator.GetMapOutputFromBlueprintLayer(configuration.AllyLayer), existing,
            configuration.PartySpawn, configuration.Buildings.Count);

	internal bool CanWalkTo(Vector3Int from, Vector3Int to)
	{
		return GridMovement.CanStep(from, to, cell => GridMovement.IsWalkable(_walkableMap, cell),
			DiagonalMovement.AllowCornerCutting);
	}

    internal AStar.Node[,] GetAStarGrid()
    {
        var grid = new AStar.Node[_walkableMap.GetLength(0),_walkableMap.GetLength(1)];
        for (int x=0;x<grid.GetLength(0);x++) for (int y=0;y<grid.GetLength(1);y++)
            grid[x,y] = new AStar.Node(x,y,_walkableMap[x,y],0);
        return grid;
    }
}
